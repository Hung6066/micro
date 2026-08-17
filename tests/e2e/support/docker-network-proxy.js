const http = require('http');

const routes = {
  5001: ['identity', 5003],
  8081: ['frontend', 8080],
  8082: ['dashboard', 8080],
  8083: ['admin', 8080],
};

for (const [port, [hostname, targetPort]] of Object.entries(routes)) {
  http.createServer((request, response) => {
    const upstream = http.request({
      hostname,
      port: targetPort,
      path: request.url,
      method: request.method,
      headers: request.headers,
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
