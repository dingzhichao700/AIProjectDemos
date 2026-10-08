// Local WeChat preview resource server. No npm dependencies required.
const http = require('node:http');
const fs = require('node:fs');
const path = require('node:path');
const os = require('node:os');

function options() {
    const result = { port: 8081, root: 'E:/testcdn' };
    const args = process.argv.slice(2);
    while (args.length) {
        const key = args.shift();
        const value = args.shift();
        if (key === '--port' && value) result.port = Number(value);
        else if (key === '--root' && value) result.root = path.resolve(value);
        else throw new Error('Usage: Start-WeChatResourceServer.bat [--root "CDN root directory"] [--port 8081]');
    }
    if (!Number.isInteger(result.port) || result.port < 1 || result.port > 65535) {
        throw new Error('Port must be an integer between 1 and 65535.');
    }
    return result;
}

try {
    const config = options();
    const root = fs.realpathSync(config.root);
    const server = http.createServer((req, res) => {
        res.setHeader('Access-Control-Allow-Origin', '*');
        res.setHeader('Access-Control-Allow-Methods', 'GET, HEAD, OPTIONS');
        res.setHeader('Cache-Control', 'no-store');
        if (req.method === 'OPTIONS') { res.writeHead(204); res.end(); return; }
        if (req.method !== 'GET' && req.method !== 'HEAD') { res.writeHead(405); res.end(); return; }
        let file;
        try {
            const pathname = decodeURIComponent(new URL(req.url, 'http://localhost').pathname);
            if (pathname.split('/').some(segment => segment.startsWith('.'))) { res.writeHead(403); res.end(); return; }
            file = path.resolve(root, '.' + pathname);
            if (!file.startsWith(root + path.sep)) { res.writeHead(403); res.end(); return; }
            file = fs.realpathSync(file);
            if (!file.startsWith(root + path.sep)) { res.writeHead(403); res.end(); return; }
        } catch { res.writeHead(404); res.end(); return; }
        fs.stat(file, (error, stat) => {
            if (error || !stat.isFile()) { res.writeHead(404); res.end(); return; }
            res.setHeader('Content-Length', stat.size);
            res.setHeader('Content-Type', file.endsWith('.json') ? 'application/json; charset=utf-8' : 'application/octet-stream');
            console.log(`${req.method} ${req.url} (${stat.size} bytes)`);
            if (req.method === 'HEAD') { res.end(); return; }
            fs.createReadStream(file).on('error', () => res.destroy()).pipe(res);
        });
    });
    server.on('error', error => {
        console.error(error.code === 'EADDRINUSE'
            ? `Port ${config.port} is already in use. Close the previous resource server, or choose --port.`
            : error.message);
        process.exitCode = 1;
    });
    server.listen(config.port, '0.0.0.0', () => {
        console.log(`Resource directory: ${root}`);
        console.log(`PC CDN: http://127.0.0.1:${config.port}`);
        for (const entries of Object.values(os.networkInterfaces())) {
            for (const address of entries || []) {
                if (address.family === 'IPv4' && !address.internal) {
                    console.log(`LAN CDN: http://${address.address}:${config.port}`);
                }
            }
        }
        console.log('Phone check: <LAN CDN>/raiden/bootstrap.json');
        console.log('Keep this window open. Close it or press Ctrl+C to stop.');
        console.log('Hot-update releases are served from the shared CDN root without restarting this script.');
    });
} catch (error) {
    console.error(error.message);
    process.exitCode = 1;
}
