const http = require('http');

const routes = {
  5001: ['identity', 5003],
  8081: ['frontend', 8080],
  8082: ['dashboard', 8080],
  8083: ['admin', 8080],
};

for (const [port, [hostname, targetPort]] of Object.entries(routes)) {
  http.createServer((request, response) => {
    // The browser container reaches this proxy via 127.0.0.1, but the
    // frontend nginx/gateway route contract is host-based and expects the
    // public localhost origin. Normalize Host so OIDC/Account routes do not
    // become a misleading 502 only inside Docker.
    const headers = { ...request.headers, host: `localhost:${port}` };
    const upstream = http.request({
      hostname,
      port: targetPort,
      path: request.url,
      method: request.method,
      headers,
    }, (upstreamResponse) => {
      response.writeHead(upstreamResponse.statusCode, upstreamResponse.headers);
      upstreamResponse.pipe(response);
    });

    upstream.on('error', (error) => {
      response.writeHead(502, { 'content-type': 'text/plain' });
      response.end(`upstream ${hostname}:${targetPort} unavailable: ${error.message}`);
    });
    request.pipe(upstream);
  }).listen(Number(port), '127.0.0.1');
}
