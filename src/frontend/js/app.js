// app.js — アプリケーション初期化

import { addRoute, startRouter } from './router.js';
import { renderHeader } from './components/header.js';
import { createPlaceholderPage } from './pages/placeholder.js';
import { renderRegisterPage } from './pages/register.js';
import { renderVideoListPage } from './pages/videoList.js';

function init() {
  // ヘッダーレンダリング
  const header = document.getElementById('site-header');
  if (header) renderHeader(header);

  // ルート登録
  addRoute('/', renderVideoListPage);
  addRoute('/videos', renderVideoListPage);
  addRoute('/register', renderRegisterPage);
  addRoute('/tags', createPlaceholderPage('タグ管理'));

  // ルーター起動
  const main = document.getElementById('main-content');
  if (main) startRouter(main);
}

document.addEventListener('DOMContentLoaded', init);
