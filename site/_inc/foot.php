</main>
<footer class="foot">
  <div>© <?= date('Y') ?> NareLauncher · бесплатный лаунчер Minecraft</div>
  <nav>
    <a href="/home/">Главная</a>
    <a href="/versions/">Версии</a>
    <a href="/download/">Скачать</a>
    <?php if (me()): ?><a href="/profile/my/">Профиль</a><?php endif; ?>
  </nav>
</footer>

<!-- Плавающая кнопка: ИИ-помощник и поддержка -->
<div id="fab-wrap">
  <div id="fab-menu" class="fab-menu">
    <button class="fab-item" id="fab-ai">🤖 ИИ-помощник</button>
    <button class="fab-item" id="fab-support">🆘 Поддержка</button>
  </div>
  <button id="fab" title="Помощь">＋</button>
</div>
<div id="ai-panel" class="chat-panel">
  <div class="cp-head">🤖 Наря · ИИ <button class="cp-x" data-close="ai-panel">✕</button></div>
  <div class="cp-body" id="ai-body"></div>
  <div id="ai-thumb" class="ai-thumb" style="display:none"></div>
  <div class="cp-foot">
    <label class="ai-clip" title="Прикрепить картинку">📎<input type="file" id="ai-img" accept="image/*" hidden></label>
    <input id="ai-in" placeholder="Спросите что угодно…"><button class="btn" id="ai-send">→</button>
  </div>
</div>
<div id="sup-panel" class="chat-panel">
  <div class="cp-head">🆘 Поддержка <button class="cp-x" data-close="sup-panel">✕</button></div>
  <div class="cp-body" id="sup-body"><p class="muted">Опишите проблему — её увидит администратор. Отвечаем в игре/на сайте.</p></div>
  <div class="cp-foot">
    <?php if (!me()): ?><input id="sup-contact" placeholder="Ваш ник или контакт" style="max-width:150px"><?php endif; ?>
    <input id="sup-in" placeholder="Сообщение в поддержку…"><button class="btn" id="sup-send">→</button>
  </div>
</div>
<script>
var SUPTOK=<?= json_encode(me() ? token() : '') ?>;
(function(){
  var fab=document.getElementById('fab'), menu=document.getElementById('fab-menu');
  fab.addEventListener('click',function(){ menu.classList.toggle('open'); fab.textContent=menu.classList.contains('open')?'✕':'＋'; });
  function openPanel(id){ document.getElementById(id).classList.add('open'); menu.classList.remove('open'); fab.textContent='＋'; }
  document.getElementById('fab-ai').onclick=function(){ openPanel('ai-panel'); if(!aiStarted)aiGreet(); };
  document.getElementById('fab-support').onclick=function(){ openPanel('sup-panel'); };
  Array.prototype.forEach.call(document.querySelectorAll('.cp-x'),function(x){ x.onclick=function(){ document.getElementById(this.getAttribute('data-close')).classList.remove('open'); }; });

  // ---- Наря: настоящий ИИ через /ai/chat, с памятью/картинками/инструментами ----
  var aiStarted=false, aiHist=[], aiBusy=false, aiImg=null;
  function esc(s){var d=document.createElement('div');d.textContent=s;return d.innerHTML;}
  // мини-markdown: **жирный**, `код`, тройные бэктики -> <pre>, переносы строк, ссылки
  function fmt(s){ s=esc(s);
    s=s.replace(/```([\s\S]*?)```/g,function(m,c){return '<pre>'+c.replace(/^\n/,'')+'</pre>';});
    s=s.replace(/`([^`]+)`/g,'<code>$1</code>');
    s=s.replace(/\*\*([^*]+)\*\*/g,'<b>$1</b>');
    s=s.replace(/(https?:\/\/[^\s<]+|\/[a-z][\w\/.\-]*)/g,'<a href="$1" target="_blank">$1</a>');
    s=s.replace(/\n/g,'<br>'); return s; }
  function add(cls,html){ var b=document.getElementById('ai-body'); var d=document.createElement('div'); d.className='cp-msg '+cls; d.innerHTML=html; b.appendChild(d); b.scrollTop=b.scrollHeight; return d; }
  function aiGreet(){ aiStarted=true; add('bot','Привет! Я <b>Наря</b> — ИИ-помощник NareLauncher. Спрашивай что угодно про лаунчер, эфиры, серверы, ботов. Я могу искать по базе, создавать каналы и ботов, смотреть присланные картинки 🖼️'); }

  // прикрепление картинки
  document.getElementById('ai-img').addEventListener('change',function(e){
    var f=e.target.files[0]; if(!f){aiImg=null;return;}
    if(f.size>5*1024*1024){ add('bot','Картинка великовата (до 5 МБ).'); this.value=''; return; }
    var rd=new FileReader(); rd.onload=function(){ aiImg=rd.result;
      var t=document.getElementById('ai-thumb'); t.style.display='block';
      t.innerHTML='<img src="'+aiImg+'"><span class="ai-thumb-x" id="ai-thumb-x">убрать ✕</span>';
      document.getElementById('ai-thumb-x').onclick=function(){ aiImg=null; t.style.display='none'; t.innerHTML=''; document.getElementById('ai-img').value=''; };
    }; rd.readAsDataURL(f);
  });

  function aiAsk(){ if(aiBusy)return; var inp=document.getElementById('ai-in'); var q=inp.value.trim();
    if(!q && !aiImg)return;
    add('me',(q?fmt(q):'')+(aiImg?'<br><img class="ai-sent" src="'+aiImg+'">':'')); inp.value='';
    var sentImg=aiImg; aiImg=null; var t=document.getElementById('ai-thumb'); t.style.display='none'; t.innerHTML='';
    aiBusy=true; var wait=add('bot','<span class="ai-typing">Наря печатает…</span>');
    fetch('/ai/chat',{method:'POST',headers:{'Content-Type':'application/json'},
      body:JSON.stringify({token:SUPTOK,text:q,image:sentImg,history:aiHist})})
      .then(function(r){return r.json();})
      .then(function(j){ aiBusy=false;
        if(j && j.configured===false){ wait.innerHTML=fmt(j.reply||'')+'<br><span class="muted" style="font-size:12px">(пока отвечаю по разделам — '+fmt(answer(q))+')</span>'; return; }
        var rep=(j&&j.reply)||'Пустой ответ.'; wait.innerHTML=fmt(rep);
        aiHist.push({role:'user',text:q}); aiHist.push({role:'ai',text:rep}); if(aiHist.length>12)aiHist=aiHist.slice(-12);
      })
      .catch(function(){ aiBusy=false; wait.innerHTML=fmt(answer(q))+'<br><span class="muted" style="font-size:12px">(сеть недоступна — ответ по разделам)</span>'; });
  }

  var KB=[
    [['скач','установ','download'],'Скачать лаунчер: раздел <a href="/download/">Скачать</a>. Запустите установщик — он поставит лаунчер сам.'],
    [['сервер','server','ядро','плагин','мод '],'Свой сервер: в лаунчере вкладка «Мои серверы» → создать → выбрать версию и ядро (ванила/Paper/Forge/Fabric/гибрid). Моды и плагины — там же. Сервера видны на сайте в разделе <a href="/servers/">Сервера</a>.'],
    [['стрим','эфир','трансл','obs','камер','микроф'],'Эфир: в лаунчере кнопка «Эфир» (студия: экран/окно/камера/микрофон, оверлей чата) или на сайте <a href="/live/go.php">начать трансляцию</a>. Ссылка на эфир постоянная: /live/категория/подкатегория/имя.'],
    [['бот','bot','python'],'Боты: раздел <a href="/bots/">Боты</a> — пишете на Python (несколько файлов), скачиваете narebot.py, запускаете python bot.py. Бот отвечает и модерирует чаты каналов/эфиров.'],
    [['канал','channel','чат'],'Каналы с общим чатом: <a href="/channels/">Каналы</a>. Владелец может задать описание, назначать модераторов, банить/мьютить.'],
    [['подпис','sub','follow'],'Подписки: на странице профиля игрока кнопка «Подписаться». Ваши подписки — <a href="/subscriptions/">тут</a>.'],
    [['монет','coin','деньг'],'Монеты начисляются за активность и ежедневные/ежеминутные награды. Баланс — в <a href="/profile/my/">профиле</a>.'],
    [['2fa','двухфактор','защит'],'2FA: Профиль → «Защита входа (2FA)». Снять — там же кнопкой «Выключить».'],
    [['удал','remote','мышк'],'Удалённое управление лаунчером: Настройки лаунчера → «Удалённое управление» (задать пароль), затем на сайте раздел «Удалёнка».'],
    [['дизайн','тем','design'],'Дизайн сайта переключается кнопкой «◑ Дизайн» в шапке: простой (светлый) и обычный (тёмный).'],
    [['верси','version'],'Версии игры — на <a href="/home/">главной</a> вкладка «Версии» и в лаунчере.'],
    [['популяр','топ','trend'],'Популярное: <a href="/popular/">тут</a> — топ видео, каналов и живых эфиров.'],
    [['поиск','найти','search'],'Поиск по видео, каналам и эфирам — <a href="/search/">страница поиска</a>.']
  ];
  function answer(q){ q=q.toLowerCase();
    for(var i=0;i<KB.length;i++){ for(var k=0;k<KB[i][0].length;k++){ if(q.indexOf(KB[i][0][k])>=0) return KB[i][1]; } }
    return 'Напишите в <b>Поддержку</b> (кнопка 🆘) — ответит администратор.'; }
  document.getElementById('ai-send').onclick=aiAsk;
  document.getElementById('ai-in').addEventListener('keydown',function(e){ if(e.key==='Enter')aiAsk(); });

  // ---- Поддержка ----
  function supSend(){ var inp=document.getElementById('sup-in'); var t=inp.value.trim(); if(!t)return;
    var contactEl=document.getElementById('sup-contact'); var contact=contactEl?contactEl.value.trim():'';
    fetch('/support/send',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({token:SUPTOK,contact:contact,text:t})})
      .then(function(r){return r.json();}).then(function(j){ var b=document.getElementById('sup-body');
        var d=document.createElement('div'); d.className='cp-msg '+(j.ok?'bot':'me'); d.innerHTML=j.ok?'✓ Отправлено! Администратор увидит ваше сообщение.':'Ошибка отправки.'; b.appendChild(d); if(j.ok)inp.value=''; b.scrollTop=b.scrollHeight; }); }
  document.getElementById('sup-send').onclick=supSend;
  document.getElementById('sup-in').addEventListener('keydown',function(e){ if(e.key==='Enter')supSend(); });
})();
(function(){
  var btn=document.getElementById('designToggle'); if(!btn)return;
  function label(){ btn.textContent=document.documentElement.classList.contains('simple')?'◑ Обычный дизайн':'◑ Простой дизайн'; }
  label();
  btn.addEventListener('click',function(){
    var s=document.documentElement.classList.toggle('simple');
    try{localStorage.setItem('nareDesign',s?'simple':'normal');}catch(e){}
    label();
  });
})();
</script>
</body>
</html>
