/* NareVisuals — страница мода.
   Список версий берётся из versions.json, того же файла, по которому
   обновляется лаунчер: один источник правды, руками ничего не дублируется. */

(function () {
  "use strict";

  /* --------------------------------------------------------------- темы */
  /* Те же семь пресетов, что и в самом моде (NareTheme.THEMES). */

  var THEMES = [
    { id: "midnight", name: "Полночь", accent: "#4f8cff", bg: "#0b0e14", soft: "#10141d" },
    { id: "coal",     name: "Уголь",   accent: "#b9c2d0", bg: "#0c0c0e", soft: "#131317" },
    { id: "sunset",   name: "Закат",   accent: "#ff8a4c", bg: "#140d09", soft: "#1c1310" },
    { id: "pine",     name: "Хвоя",    accent: "#46d08a", bg: "#081210", soft: "#0e1b18" },
    { id: "lilac",    name: "Сирень",  accent: "#b184ff", bg: "#0e0a18", soft: "#161022" },
    { id: "cherry",   name: "Вишня",   accent: "#ff5470", bg: "#140a0e", soft: "#1d1016" }
  ];

  var KEY = "narevisuals-theme";

  function applyTheme(id) {
    var t = null;
    for (var i = 0; i < THEMES.length; i++) if (THEMES[i].id === id) t = THEMES[i];
    if (!t) t = THEMES[0];

    var s = document.documentElement.style;
    s.setProperty("--accent", t.accent);
    s.setProperty("--bg", t.bg);
    s.setProperty("--bg-soft", t.soft);
    document.body.dataset.theme = t.id;

    var buttons = document.querySelectorAll("#themes button");
    for (var j = 0; j < buttons.length; j++)
      buttons[j].setAttribute("aria-pressed", buttons[j].dataset.theme === t.id ? "true" : "false");

    try { localStorage.setItem(KEY, t.id); } catch (e) { /* приватный режим — не беда */ }
  }

  function buildThemes() {
    var box = document.getElementById("themes");
    if (!box) return;

    THEMES.forEach(function (t) {
      var b = document.createElement("button");
      b.type = "button";
      b.dataset.theme = t.id;
      b.title = t.name;
      b.setAttribute("aria-label", "Тема «" + t.name + "»");
      b.style.setProperty("--sw", t.accent);
      b.addEventListener("click", function () { applyTheme(t.id); });
      box.appendChild(b);
    });

    var saved = null;
    try { saved = localStorage.getItem(KEY); } catch (e) { /* ничего */ }
    applyTheme(saved || "midnight");
  }

  /* ------------------------------------------------------------- версии */

  function size(bytes) {
    if (!bytes) return "";
    var kb = bytes / 1024;
    return kb < 1024 ? Math.round(kb) + " КБ" : (kb / 1024).toFixed(1) + " МБ";
  }

  function renderReleases(data) {
    var box = document.getElementById("releases");
    var list = (data && data.releases) || [];
    if (!list.length) throw new Error("пусто");

    box.innerHTML = "";

    list.forEach(function (r, i) {
      var row = document.createElement("div");
      row.className = "rel";

      var v = document.createElement("span");
      v.className = "rel-v";
      v.textContent = r.version;
      row.appendChild(v);

      if (i === 0) {
        var tag = document.createElement("span");
        tag.className = "tag";
        tag.textContent = "последняя";
        row.appendChild(tag);
      }

      var notes = document.createElement("span");
      notes.className = "rel-notes";
      notes.textContent = r.notes || "";
      row.appendChild(notes);

      var sz = document.createElement("span");
      sz.className = "rel-size";
      sz.textContent = size(r.size);
      row.appendChild(sz);

      var a = document.createElement("a");
      a.className = "rel-get";
      a.href = r.file;              // файл лежит рядом с этой страницей
      a.textContent = "Скачать";
      a.setAttribute("download", "");
      row.appendChild(a);

      box.appendChild(row);
    });

    // кнопка в шапке ведёт прямо на свежий jar
    var top = document.getElementById("dl-mod");
    var meta = document.getElementById("dl-mod-meta");
    var latest = list[0];
    if (top && latest) {
      top.href = latest.file;
      top.setAttribute("download", "");
      if (meta) meta.textContent = latest.version + " · " + size(latest.size) + " · Forge 1.21.4";
    }
  }

  function loadReleases() {
    var box = document.getElementById("releases");
    if (!box) return;

    fetch("versions.json", { cache: "no-cache" })
      .then(function (r) {
        if (!r.ok) throw new Error(r.status);
        return r.json();
      })
      .then(renderReleases)
      .catch(function () {
        box.innerHTML = "";
        var p = document.createElement("p");
        p.className = "failed";
        p.textContent = "Список версий не загрузился. ";
        var a = document.createElement("a");
        a.href = "https://github.com/Winnigames2024-Original/mods/tree/main/narevisuals";
        a.textContent = "Открыть папку на GitHub";
        p.appendChild(a);
        box.appendChild(p);

        var meta = document.getElementById("dl-mod-meta");
        if (meta) meta.textContent = "смотрите список ниже";
      });
  }

  /* ------------------------------------------------- копирование config */

  function wireCopy() {
    var buttons = document.querySelectorAll(".copy");
    for (var i = 0; i < buttons.length; i++) {
      buttons[i].addEventListener("click", function () {
        var btn = this;
        var code = btn.parentNode.querySelector("code");
        if (!code) return;

        var done = function () {
          btn.textContent = "скопировано";
          btn.classList.add("done");
          setTimeout(function () {
            btn.textContent = "копировать";
            btn.classList.remove("done");
          }, 1600);
        };

        if (navigator.clipboard && navigator.clipboard.writeText) {
          navigator.clipboard.writeText(code.textContent).then(done, function () {
            btn.textContent = "не вышло";
          });
        } else {
          var sel = window.getSelection();
          var range = document.createRange();
          range.selectNodeContents(code);
          sel.removeAllRanges();
          sel.addRange(range);
          btn.textContent = "выделено, Ctrl+C";
        }
      });
    }
  }

  /* ------------------------------------------------------ появление блоков */

  function wireReveal() {
    if (!("IntersectionObserver" in window)) return;

    var items = document.querySelectorAll(".card, .steps li, .rel, .band h2, .band-lead");
    for (var i = 0; i < items.length; i++) items[i].classList.add("reveal");

    var io = new IntersectionObserver(function (entries) {
      entries.forEach(function (e) {
        if (!e.isIntersecting) return;
        e.target.classList.add("seen");
        io.unobserve(e.target);
      });
    }, { rootMargin: "0px 0px -8% 0px", threshold: .08 });

    for (var j = 0; j < items.length; j++) io.observe(items[j]);
  }

  /* --------------------------------------------------------------- старт */

  buildThemes();
  loadReleases();
  wireCopy();
  wireReveal();
})();
