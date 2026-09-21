// Loopback-only gateway for temporary Shopify authorization. No admin CRUD/files.
const http = require('node:http');
const fs = require('node:fs');
const path = require('node:path');
const root = path.resolve(__dirname, '../artifacts/woocommerce-tunnel-web/browser');
const mime = {'.html':'text/html', '.js':'text/javascript', '.css':'text/css', '.svg':'image/svg+xml', '.png':'image/png', '.ico':'image/x-icon', '.woff2':'font/woff2'};
const attempts = new Map();
setInterval(() => attempts.clear(), 60000).unref();
http.createServer((req,res) => {
  res.setHeader('X-Content-Type-Options','nosniff');
  res.setHeader('Referrer-Policy','no-referrer');
  res.setHeader('Cache-Control','no-store');
  let url;
  try { url = new URL(req.url, 'http://localhost'); } catch { res.writeHead(400).end(); return; }
  const p = url.pathname;
  const allowed = p.startsWith('/api/integrations/shopify/') ||
    (req.method === 'POST' && p === '/api/auth/login') ||
    (req.method === 'GET' && (/^\/api\/lookups\/by-type\/\d+$/.test(p) || /^\/api\/rolepermissions\/\d+$/i.test(p) || p === '/api/profile/me'));
  if (allowed) {
    const key = String(req.headers['cf-connecting-ip'] || req.socket.remoteAddress);
    const count = (attempts.get(key) || 0) + 1; attempts.set(key,count);
    if(count > 120) { res.writeHead(429).end('Retry in one minute'); return; }
    // Port 52984 is reserved by Windows on this host; the Shopify-only backend runs on 5123.
    const headers = {...req.headers, host:'localhost:5123'};
    for (const h of ['forwarded','x-forwarded-host','x-forwarded-proto','x-forwarded-for','connection']) delete headers[h];
    const upstream = http.request({host:'localhost',port:5123,path:req.url,method:req.method,headers}, response => {
      res.writeHead(response.statusCode, {...response.headers,'cache-control':'no-store'}); response.pipe(res);
    });
    upstream.setTimeout(30000,()=>upstream.destroy());
    upstream.on('error',()=>{ if(!res.headersSent) res.writeHead(502); res.end('Local OMS unavailable'); });
    req.pipe(upstream); return;
  }
  if (p.startsWith('/api/') || p.startsWith('/hubs/') || p.startsWith('/uploads/') || p.startsWith('/swagger')) { res.writeHead(404).end(); return; }
  if (!['GET','HEAD'].includes(req.method)) { res.writeHead(405).end(); return; }
  let file;
  try { file = path.resolve(root, '.' + decodeURIComponent(p)); } catch { res.writeHead(400).end(); return; }
  if (!file.startsWith(root + path.sep)) file = path.join(root,'index.html');
  if (!fs.existsSync(file) || !fs.statSync(file).isFile()) file = path.join(root,'index.html');
  res.setHeader('Content-Type',mime[path.extname(file)] || 'application/octet-stream');
  if(req.method === 'HEAD') res.end(); else fs.createReadStream(file).pipe(res);
}).listen(5523,'127.0.0.1',()=>console.log('Shopify gateway listening on 127.0.0.1:5523'));

