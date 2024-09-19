using System.Text.Json;
using SIPSorcery.Net;
using SIPSorceryMedia.FFmpeg;
using WebSocketSharp;
using Monitor = SIPSorceryMedia.FFmpeg.Monitor;

namespace ScreenShare;

public class App
{
    private readonly Monitor _monitor;

    private readonly RTCPeerConnection _pc = new(new()
    {
        bundlePolicy = RTCBundlePolicy.balanced,
        iceServers = [new() { urls = "stun:stun.l.google.com:19302" }]
    });

    private readonly WebSocket _ws;

    public App(string id, Monitor monitor)
    {
        _monitor = monitor;
        _ws = new("wss://icy.cx/signal/" + id);
        _ws.Connect();

        _ws.OnMessage += HandleMessage;
        _ws.OnClose += (_, _) => Disconnect();

        _pc.onicecandidate += candidate => _ws.Send(candidate.toJSON());
    }

    public bool Closed { get; private set; }

    public event EventHandler? OnClosed;

    private void StartVideo()
    {
        FFmpegScreenSource screen = new(_monitor.Path, _monitor.Rect, 30);
        MediaStreamTrack track = new(screen.GetVideoSourceFormats(), MediaStreamStatusEnum.SendOnly);

        screen.SetVideoEncoderBitrate(2_000_000); // 2mb/s
        _pc.addTrack(track);

        screen.OnVideoSourceEncodedSample += _pc.SendVideo;
        _pc.OnVideoFormatsNegotiated += formats => screen.SetVideoSourceFormat(formats.First());

        _pc.onconnectionstatechange += async state =>
        {
            Console.WriteLine($"Peer connection state change to {state}.");

            switch (state)
            {
                case RTCPeerConnectionState.connected:
                    await screen.StartVideo();
                    break;
                case RTCPeerConnectionState.failed:
                    _pc.Close("ice disconnection");
                    break;
                case RTCPeerConnectionState.closed:
                    await screen.CloseVideo();
                    screen.Dispose();
                    break;
            }
        };
    }

    private async Task CreateOffer()
    {
        StartVideo();
        var offer = _pc.createOffer(new() { X_ExcludeIceCandidates = true });
        await _pc.setLocalDescription(offer);

        _ws.Send(offer.toJSON());
    }

    // private async Task CreateAnswer(string offer)
    // {
    //     SetRemoteDescription(offer);
    //
    //     StartVideo();
    //     var answer = _pc.createAnswer();
    //     await _pc.setLocalDescription(answer);
    //
    //     _ws.Send(answer.toJSON());
    // }

    private void SetRemoteDescription(string desc)
    {
        if (!RTCSessionDescriptionInit.TryParse(desc, out var description))
            throw new ArgumentException(null, nameof(desc));

        _pc.setRemoteDescription(description);
    }

    private void AddCandidate(string candidate)
    {
        if (!RTCIceCandidateInit.TryParse(candidate, out var ice))
            throw new ArgumentException(null, nameof(candidate));

        _pc.addIceCandidate(ice);
    }

    private void Disconnect(string reason = "Remote disconnected")
    {
        _pc.Close(reason);
        Closed = true;
        OnClosed?.Invoke(this, EventArgs.Empty);
    }

    private void HandleMessage(object? sender, MessageEventArgs args)
    {
        var msg = JsonSerializer.Deserialize<Dictionary<string, JsonElement?>>(args.Data);
        string? type = msg!.GetValueOrDefault("type")?.ToString();
        string? candidate = msg!.GetValueOrDefault("candidate")?.ToString();

        if (type == "connected")
            _ = CreateOffer();
        else if (type == "offer")
            // We're not accepting offers, so a new one is made.
            _ = CreateOffer();
        else if (type == "answer")
            SetRemoteDescription(args.Data);
        else if (candidate is not null)
            AddCandidate(args.Data);
        else if (type == "disconnected")
            Disconnect();
    }
}