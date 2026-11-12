/*
 * NareQR — маленький генератор QR-кодов на чистом JS, без зависимостей.
 * Режим: байтовый (UTF-8), уровень коррекции L, версии 1–6, маска 0.
 * Этого хватает для коротких ссылок (до ~130 байт). Рисует в <canvas>.
 *
 * Делаем своё, чтобы капча была полностью на нашем сайте и ни от кого не зависела.
 */
(function (global) {
  'use strict';

  // --- GF(256), примитивный полином 0x11d ---
  var EXP = new Array(512), LOG = new Array(256);
  (function () {
    var x = 1;
    for (var i = 0; i < 255; i++) { EXP[i] = x; LOG[x] = i; x <<= 1; if (x & 0x100) x ^= 0x11d; }
    for (var j = 255; j < 512; j++) EXP[j] = EXP[j - 255];
  })();
  function gmul(a, b) { return (a === 0 || b === 0) ? 0 : EXP[LOG[a] + LOG[b]]; }

  // версия -> { size, ecPerBlock, dataCodewords } для уровня L (один блок)
  var VERS = {
    1: { ec: 7,  data: 19,  align: [] },
    2: { ec: 10, data: 34,  align: [18] },
    3: { ec: 15, data: 55,  align: [22] },
    4: { ec: 20, data: 80,  align: [26] },
    5: { ec: 26, data: 108, align: [30] },
    6: { ec: 18, data: 136, align: [34] }   // v6-L: 2 блока по 68 — обрабатываем отдельно
  };

  function sizeOf(v) { return 17 + 4 * v; }

  function rsGen(n) {
    var g = [1];
    for (var i = 0; i < n; i++) {
      var ng = new Array(g.length + 1);
      for (var k = 0; k < ng.length; k++) ng[k] = 0;
      for (var j = 0; j < g.length; j++) {
        ng[j] ^= g[j];
        ng[j + 1] ^= gmul(g[j], EXP[i]);
      }
      g = ng;
    }
    return g;
  }

  function rsEncode(data, n) {
    var gen = rsGen(n);
    var res = data.slice();
    for (var i = 0; i < n; i++) res.push(0);
    for (var p = 0; p < data.length; p++) {
      var coef = res[p];
      if (coef !== 0) for (var j = 0; j < gen.length; j++) res[p + j] ^= gmul(gen[j], coef);
    }
    return res.slice(data.length);
  }

  function utf8(str) {
    var out = [], i, c;
    for (i = 0; i < str.length; i++) {
      c = str.charCodeAt(i);
      if (c < 0x80) out.push(c);
      else if (c < 0x800) { out.push(0xc0 | (c >> 6), 0x80 | (c & 0x3f)); }
      else { out.push(0xe0 | (c >> 12), 0x80 | ((c >> 6) & 0x3f), 0x80 | (c & 0x3f)); }
    }
    return out;
  }

  function pickVersion(len) {
    for (var v = 1; v <= 6; v++) if (VERS[v].data >= len + 2) return v;
    throw new Error('слишком длинные данные для QR');
  }

  // данные -> массив кодовых слов (байтов) с учётом блоков
  function buildData(bytes, v) {
    var info = VERS[v];
    var bits = [];
    function put(val, n) { for (var i = n - 1; i >= 0; i--) bits.push((val >> i) & 1); }
    put(0x4, 4);                 // байтовый режим
    put(bytes.length, 8);        // счётчик (версии 1–9: 8 бит)
    for (var i = 0; i < bytes.length; i++) put(bytes[i], 8);
    // терминатор
    var cap = info.data * 8;
    for (var t = 0; t < 4 && bits.length < cap; t++) bits.push(0);
    while (bits.length % 8 !== 0) bits.push(0);
    var cw = [];
    for (var b = 0; b < bits.length; b += 8) {
      var byte = 0; for (var k = 0; k < 8; k++) byte = (byte << 1) | bits[b + k];
      cw.push(byte);
    }
    var pad = [0xec, 0x11], pi = 0;
    while (cw.length < info.data) { cw.push(pad[pi & 1]); pi++; }
    return cw;
  }

  // финальная последовательность кодовых слов (данные+коррекция), с чередованием блоков для v6
  function finalCodewords(dataCw, v) {
    var info = VERS[v];
    if (v < 6) {
      var ec = rsEncode(dataCw, info.ec);
      return dataCw.concat(ec);
    }
    // v6-L: 2 блока по 68 данных, по 18 EC
    var b1 = dataCw.slice(0, 68), b2 = dataCw.slice(68, 136);
    var e1 = rsEncode(b1, 18), e2 = rsEncode(b2, 18);
    var out = [], i;
    for (i = 0; i < 68; i++) { out.push(b1[i]); out.push(b2[i]); }
    for (i = 0; i < 18; i++) { out.push(e1[i]); out.push(e2[i]); }
    return out;
  }

  // --- матрица ---
  function newMatrix(v) {
    var n = sizeOf(v), m = [], fn = [], r, c;
    for (r = 0; r < n; r++) { m[r] = []; fn[r] = []; for (c = 0; c < n; c++) { m[r][c] = 0; fn[r][c] = false; } }
    function set(r, c, val) { m[r][c] = val ? 1 : 0; fn[r][c] = true; }

    function finder(r0, c0) {
      for (var r = -1; r <= 7; r++) for (var c = -1; c <= 7; c++) {
        var rr = r0 + r, cc = c0 + c;
        if (rr < 0 || cc < 0 || rr >= n || cc >= n) continue;
        var on = (r >= 0 && r <= 6 && (c === 0 || c === 6)) ||
                 (c >= 0 && c <= 6 && (r === 0 || r === 6)) ||
                 (r >= 2 && r <= 4 && c >= 2 && c <= 4);
        set(rr, cc, on ? 1 : 0);
      }
    }
    finder(0, 0); finder(0, n - 7); finder(n - 7, 0);

    // тайминги
    for (var i = 8; i < n - 8; i++) { set(6, i, i % 2 === 0 ? 1 : 0); set(i, 6, i % 2 === 0 ? 1 : 0); }

    // тёмный модуль
    set(4 * v + 9, 8, 1);

    // выравнивающие
    var al = VERS[v].align;
    for (var a = 0; a < al.length; a++) {
      var cr = al[a], cc0 = al[a];
      for (var dr = -2; dr <= 2; dr++) for (var dc = -2; dc <= 2; dc++) {
        var on = (Math.abs(dr) === 2 || Math.abs(dc) === 2 || (dr === 0 && dc === 0)) ? 1 : 0;
        set(cr + dr, cc0 + dc, on);
      }
    }

    // зарезервировать области формата (заполним позже)
    for (var k = 0; k <= 8; k++) { if (!fn[8][k]) fn[8][k] = true; if (!fn[k][8]) fn[k][8] = true; }
    for (var k2 = 0; k2 < 8; k2++) { fn[8][n - 1 - k2] = true; fn[n - 1 - k2][8] = true; }

    return { n: n, m: m, fn: fn };
  }

  function placeData(mat, cw) {
    var n = mat.n, m = mat.m, fn = mat.fn;
    var bits = [];
    for (var i = 0; i < cw.length; i++) for (var b = 7; b >= 0; b--) bits.push((cw[i] >> b) & 1);
    var idx = 0, up = true;
    for (var col = n - 1; col > 0; col -= 2) {
      if (col === 6) col = 5;                 // пропускаем вертикальный тайминг
      for (var t = 0; t < n; t++) {
        var row = up ? n - 1 - t : t;
        for (var s = 0; s < 2; s++) {
          var c = col - s;
          if (fn[row][c]) continue;
          var bit = idx < bits.length ? bits[idx++] : 0;
          // маска 0: (row+col)%2==0 -> инверт
          if ((row + c) % 2 === 0) bit ^= 1;
          m[row][c] = bit;
        }
      }
      up = !up;
    }
  }

  function formatBits() {
    // уровень L = 01, маска 0 = 000 -> data = 0b01000
    var data = (1 << 3) | 0;
    var v = data << 10;
    var g = 0x537;
    for (var i = 14; i >= 10; i--) if ((v >> i) & 1) v ^= g << (i - 10);
    var full = ((data << 10) | (v & 0x3ff)) ^ 0x5412;
    return full & 0x7fff;   // 15 бит
  }

  function placeFormat(mat) {
    var n = mat.n, m = mat.m, f = formatBits();
    for (var i = 0; i < 15; i++) {
      var mod = (f >> i) & 1;
      // вертикаль (столбец 8)
      if (i < 6) m[i][8] = mod;
      else if (i < 8) m[i + 1][8] = mod;
      else m[n - 15 + i][8] = mod;
      // горизонталь (строка 8)
      if (i < 8) m[8][n - i - 1] = mod;
      else if (i < 9) m[8][15 - i - 1 + 1] = mod;
      else m[8][15 - i - 1] = mod;
    }
    m[n - 8][8] = 1;   // всегда тёмный модуль
  }

  function matrix(text) {
    var bytes = utf8(text);
    var v = pickVersion(bytes.length);
    var dataCw = buildData(bytes, v);
    var cw = finalCodewords(dataCw, v);
    var mat = newMatrix(v);
    placeData(mat, cw);
    placeFormat(mat);
    return mat.m;
  }

  function draw(canvas, text, opts) {
    opts = opts || {};
    var m = matrix(text);
    var n = m.length;
    var quiet = opts.quiet != null ? opts.quiet : 4;
    var scale = opts.scale || 6;
    var total = (n + quiet * 2) * scale;
    canvas.width = total; canvas.height = total;
    var ctx = canvas.getContext('2d');
    ctx.fillStyle = opts.light || '#ffffff';
    ctx.fillRect(0, 0, total, total);
    ctx.fillStyle = opts.dark || '#000000';
    for (var r = 0; r < n; r++) for (var c = 0; c < n; c++) {
      if (m[r][c]) ctx.fillRect((c + quiet) * scale, (r + quiet) * scale, scale, scale);
    }
    return canvas;
  }

  function _debug(text) {
    var bytes = utf8(text);
    var v = pickVersion(bytes.length);
    var dataCw = buildData(bytes, v);
    return { v: v, dataCw: dataCw, cw: finalCodewords(dataCw, v) };
  }

  global.NareQR = { matrix: matrix, draw: draw, _debug: _debug };
})(typeof window !== 'undefined' ? window : this);
