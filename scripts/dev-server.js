#!/usr/bin/env node
// ローカル開発用プロキシサーバー
// フロントエンドを http://localhost:3000 で配信し、
// /api/* へのリクエストを http://localhost:7071/api/* にプロキシする

import http from 'http';
import https from 'https';
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const FRONTEND_DIR = path.resolve(__dirname, '../src/frontend');
const API_HOST = 'localhost';
const API_PORT = 7071;
const DEV_PORT = 3000;

const MIME_TYPES = {
  '.html': 'text/html',
  '.js':   'application/javascript',
  '.css':  'text/css',
  '.json': 'application/json',
  '.png':  'image/png',
  '.svg':  'image/svg+xml',
  '.ico':  'image/x-icon',
  '.woff2': 'font/woff2',
};

function serveStatic(req, res) {
  let urlPath = req.url.split('?')[0];
  // SPA フォールバック: 拡張子なしのパスは index.html を返す
  if (!path.extname(urlPath)) {
    urlPath = '/index.html';
  }

  const filePath = path.join(FRONTEND_DIR, urlPath);

  // ディレクトリトラバーサル防止
  if (!filePath.startsWith(FRONTEND_DIR)) {
    res.writeHead(403);
    res.end('Forbidden');
    return;
  }

  fs.readFile(filePath, (err, data) => {
    if (err) {
      res.writeHead(404);
      res.end(`Not Found: ${urlPath}`);
      return;
    }
    const ext = path.extname(filePath);
    res.writeHead(200, { 'Content-Type': MIME_TYPES[ext] ?? 'application/octet-stream' });
    res.end(data);
  });
}

function proxyToApi(req, res) {
  const options = {
    hostname: API_HOST,
    port: API_PORT,
    path: req.url,
    method: req.method,
    headers: { ...req.headers, host: `${API_HOST}:${API_PORT}` },
  };

  const proxyReq = http.request(options, (proxyRes) => {
    res.writeHead(proxyRes.statusCode, proxyRes.headers);
    proxyRes.pipe(res);
  });

  proxyReq.on('error', (err) => {
    console.error(`[proxy error] ${req.url}: ${err.message}`);
    res.writeHead(502);
    res.end(JSON.stringify({ error: 'API サーバーに接続できません。func start が起動しているか確認してください。' }));
  });

  req.pipe(proxyReq);
}

const server = http.createServer((req, res) => {
  if (req.url.startsWith('/api/')) {
    proxyToApi(req, res);
  } else {
    serveStatic(req, res);
  }
});

server.listen(DEV_PORT, () => {
  console.log(`\n  フロントエンド: http://localhost:${DEV_PORT}`);
  console.log(`  API プロキシ:   /api/* → http://localhost:${API_PORT}/api/*\n`);
});
