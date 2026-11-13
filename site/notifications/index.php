<?php
require_once __DIR__ . '/../_inc/config.php';
require_login();
$PAGE_TITLE = 'Уведомления — NareLauncher';
require __DIR__ . '/../_inc/head.php';
?>
<h1>🔔 Уведомления</h1>
<p class="muted">Здесь — события о вас (подписки, лайки, зрители эфиров) и рассылки. Разрешите уведомления в браузере (колокольчик в шапке), чтобы они приходили на телефон/рабочий стол.</p>

<div class="panel" id="proPanel" style="margin-top:12px">
  <div id="proInfo" class="muted">Загрузка…</div>
  <div id="buyWrap" style="display:none;margin-top:10px">
    <button class="btn" id="buyBtn">Купить подписку на рассылку — <b>10000</b> монет</button>
    <p class="muted" style="font-size:13px;margin:8px 0 0">Подписка даёт право слать уведомление <b>всем или конкретному пользователю раз в 10 минут</b>. Админы — раз в секунду, без покупки.</p>
  </div>
  <div id="sendWrap" style="display:none;margin-top:12px">
    <h3 style="margin:0 0 8px">📣 Отправить уведомление</h3>
    <div class="row-form">
      <select id="nTarget" style="background:#0a0d14;border:1px solid var(--border);color:var(--text);border-radius:9px;padding:9px 12px">
        <option value="all">Всем</option>
        <option value="one">Конкретному пользователю</option>
      </select>
      <input id="nNick" placeholder="ник получателя" maxlength="16" style="display:none;flex:0 0 180px">
      <input id="nText" placeholder="Текст уведомления…" maxlength="300" style="flex:1;min-width:200px">
      <button class="btn" id="nSend">Отправить</button>
    </div>
    <div class="muted" id="nSt" style="font-size:13px;margin-top:8px"></div>
  </div>
</div>

<h2 style="margin-top:22px">Лента событий</h2>
<div id="evFeed" class="vlist"><p class="muted">Загрузка…</p></div>

<script>
var TOK=<?= json_encode(token()) ?>;
function post(u,d){return fetch(u,{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(d||{})}).then(function(r){return r.json();});}
function esc(s){var d=document.createElement('div');d.textContent=s==null?'':s;return d.innerHTML;}
function ago(ts){var s=Math.floor((Date.now()-ts)/1000); if(s<60)return 'только что'; if(s<3600)return Math.floor(s/60)+' мин назад'; if(s<86400)return Math.floor(s/3600)+' ч назад'; return Math.floor(s/86400)+' дн назад';}
var ICON={'подписка':'➕','отписка':'➖','лайк':'❤️','анлайк':'💔','зритель':'👁','ушёл':'🚪','рассылка':'📣'};

function loadStatus(){
  post('/notify/status',{token:TOK}).then(function(j){
    if(!j.ok)return;
    var info=document.getElementById('proInfo');
    if(j.admin){ info.innerHTML='<b style="color:var(--green)">Вы администратор</b> — можно слать уведомления всем/кому-то <b>раз в секунду</b>.';
      document.getElementById('buyWrap').style.display='none'; document.getElementById('sendWrap').style.display='block'; }
    else if(j.pro){ var left=Math.max(0,Math.ceil((j.until-Date.now())/86400000));
      info.innerHTML='<b style="color:var(--green)">Подписка активна</b> — осталось ~'+left+' дн. Можно слать уведомления раз в 10 минут. Монет: <b>'+j.coins+'</b>.';
      document.getElementById('buyWrap').style.display='none'; document.getElementById('sendWrap').style.display='block'; }
    else { info.innerHTML='Подписки нет. Монет у вас: <b>'+j.coins+'</b>.';
      document.getElementById('buyWrap').style.display='block'; document.getElementById('sendWrap').style.display='none'; }
    // лента
    var f=document.getElementById('evFeed');
    if(!j.events||!j.events.length){ f.innerHTML='<p class="muted">Пока событий нет. Когда на вас подпишутся, лайкнут видео или зайдут на эфир — появится тут.</p>'; return; }
    f.innerHTML=j.events.map(function(e){
      return '<div class="vrow"><span>'+(ICON[e.type]||'🔔')+' '+esc(e.text)+'</span><span class="muted" style="font-size:12px">'+ago(e.at)+'</span></div>';
    }).join('');
  });
}
document.getElementById('buyBtn').onclick=function(){
  if(!confirm('Купить подписку на рассылку за 10000 монет?'))return;
  post('/notify/buy',{token:TOK}).then(function(j){ if(j.ok){alert('Подписка активна!');loadStatus();} else alert(j.error||'Ошибка'); });
};
document.getElementById('nTarget').onchange=function(){ document.getElementById('nNick').style.display=this.value==='one'?'block':'none'; };
document.getElementById('nSend').onclick=function(){
  var t=document.getElementById('nText').value.trim(); if(!t)return;
  var tgt=document.getElementById('nTarget').value==='one'?document.getElementById('nNick').value.trim():'all';
  var st=document.getElementById('nSt'); st.textContent='Отправка…';
  post('/notify/send',{token:TOK,text:t,target:tgt}).then(function(j){
    if(j.ok){ st.innerHTML='<b style="color:var(--green)">Отправлено'+(j.target==='all'?' всем':(' — '+esc(j.target)))+' ✓</b>'; document.getElementById('nText').value=''; }
    else st.innerHTML='<b style="color:var(--red)">'+esc(j.error||'Ошибка')+'</b>';
  });
};
loadStatus(); setInterval(loadStatus, 20000);
</script>
<?php require __DIR__ . '/../_inc/foot.php'; ?>
