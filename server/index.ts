const signalRegex = /^\/signal\/(\w+)\/?$/;

const globalCount = new Map<string, number>();

const server = Bun.serve<{ id: string }>({
  fetch(req, server) {
    const url = new URL(req.url);
    const id = url.pathname.match(signalRegex)?.[1];

    console.log(new Date(), "[  REQ  ]", req.url, id);

    if (id && server.upgrade(req, { data: { id } })) return;

    if (url.pathname === "/signal" || url.pathname === "/signal/")
      return new Response(null, {
        status: 302,
        headers: {
          location: "https://files.icyuba.com/webrtc_screenshare.html",
        },
      });
    return new Response("Not Found", { status: 404 });
  },

  websocket: {
    open(ws) {
      const id = ws.data.id;
      const count = globalCount.get(id) ?? 0;

      console.log(new Date(), "[  NEW  ]", id, `(${count + 1})`);

      if (count >= 2)
        return void ws.close(
          4000,
          "2 clients are already connected to this id"
        );

      globalCount.set(id, count + 1);

      ws.subscribe(id);
      ws.subscribe("$");
      ws.publish(id, JSON.stringify({ type: "connected" }));
    },

    message(ws, message) {
      const id = ws.data.id;

      console.log(new Date(), "[MESSAGE]", id, message);

      if (typeof message !== "string") {
        ws.publish(id, JSON.stringify({ type: "disconnected" }));
        return void ws.close(4001, "Invalid message type");
      }

      ws.publish(id, message);
    },

    close(ws, code, message) {
      const id = ws.data.id;
      const count = globalCount.get(id) ?? 1;

      console.log(new Date(), "[ CLOSE ]", id, `(${count - 1})`, code, message);

      if (count > 0) globalCount.set(id, count - 1);

      ws.publish(id, JSON.stringify({ type: "disconnected" }));
    },
  },
});

// Send a ping every minute to prevent Cloudflare from closing the connection
setInterval(
  () => server.publish("$", JSON.stringify({ type: "ping" })),
  60_000
);
