// main.js — SPA entry point

const app = {
  init() {
    this.render();
  },

  render() {
    const main = document.getElementById('main-content');
    const p = document.createElement('p');
    p.textContent = 'X Video Collector へようこそ。';
    main.appendChild(p);
  },
};

document.addEventListener('DOMContentLoaded', () => app.init());
