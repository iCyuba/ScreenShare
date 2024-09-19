FROM oven/bun:1.1-slim AS build
WORKDIR /app

COPY package.json ./
RUN bun install

COPY index.ts .

RUN bun build index.ts --compile --outfile server

FROM debian:bullseye-slim AS runtime
COPY --from=build /app/server .

CMD ["./server"]
