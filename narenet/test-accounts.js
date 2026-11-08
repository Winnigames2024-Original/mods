'use strict';
const http = require('http');
const PORT = 24452;
let fails = 0;
function check(ok, what){ console.log((ok?'ok   ':'FAIL ')+what); if(!ok) fails++; }

function call(method, route, body){
  return new Promise(resolve=>{
    const data = body===undefined?null:Buffer.from(JSON.stringify(body));
    const req = http.request({host:'127.0.0.1',port:PORT,path:route,method,
      headers:data?{'Content-Type':'application/json','Content-Length':data.length}:{}},res=>{
      const parts=[]; res.on('data',c=>parts.push(c));
      res.on('end',()=>{let j=null;try{j=JSON.parse(Buffer.concat(parts).toString());}catch(e){}resolve({code:res.statusCode,json:j});});
    });
    req.on('error',e=>resolve({code:0,error:e.message}));
    if(data) req.write(data); req.end();
  });
}

(async()=>{
  // админ заведён из переменной окружения
  let r = await call('POST','/account/login',{nick:'Winnigames2024',password:'adminpass'});
  check(r.code===200 && r.json.admin===true, 'админ входит и помечен админом');
  const admin = r.json.token;

  // обычная регистрация
  r = await call('POST','/account/register',{nick:'Petya',password:'1234'});
  check(r.code===200 && r.json.token, 'игрок зарегистрирован');
  check(r.json.admin===false, 'игрок не админ');

  r = await call('POST','/account/register',{nick:'Petya',password:'xxxx'});
  check(r.code===409, 'повторный ник занят');

  r = await call('POST','/account/register',{nick:'ab',password:'1234'});
  check(r.code===400, 'короткий ник отклонён');
  r = await call('POST','/account/register',{nick:'Masha',password:'12'});
  check(r.code===400, 'короткий пароль отклонён');

  // вход
  r = await call('POST','/account/login',{nick:'Petya',password:'1234'});
  check(r.code===200, 'вход верным паролем');
  r = await call('POST','/account/login',{nick:'Petya',password:'0000'});
  check(r.code===403, 'вход неверным паролем отклонён');

  // смена пароля
  r = await call('POST','/account/password',{nick:'Petya',password:'1234',newPassword:'5678'});
  check(r.code===200, 'пароль сменён');
  r = await call('POST','/account/login',{nick:'Petya',password:'1234'});
  check(r.code===403, 'старый пароль больше не работает');
  r = await call('POST','/account/login',{nick:'Petya',password:'5678'});
  check(r.code===200, 'новый пароль работает');

  // статус — чист
  r = await call('GET','/status?nick=Petya');
  check(r.code===200 && r.json.banned===false, 'Petya не забанен');

  // не-админ не может банить
  const petyaTok = (await call('POST','/account/login',{nick:'Petya',password:'5678'})).json.token;
  r = await call('POST','/admin/ban',{token:petyaTok,nick:'Vasya',minutes:10,reason:'x'});
  check(r.code===403, 'игрок не может банить');

  // админ банит на время
  r = await call('POST','/admin/ban',{token:admin,nick:'Petya',minutes:60,reason:'читы'});
  check(r.code===200, 'админ забанил на час');
  r = await call('GET','/status?nick=Petya');
  check(r.json.banned===true && r.json.appealOnly===true, 'статус: бан, только апелляция');
  check(r.json.banReason==='читы', 'причина бана видна: '+r.json.banReason);
  check(r.json.until>0 && /ещё/.test(r.json.banLeft), 'срок виден: '+r.json.banLeft);

  // бан навсегда
  r = await call('POST','/admin/ban',{token:admin,nick:'Petya',minutes:0,reason:'грифер'});
  r = await call('GET','/status?nick=Petya');
  check(r.json.until===0 && r.json.banLeft==='навсегда', 'бан навсегда: '+r.json.banLeft);

  // апелляция
  r = await call('POST','/appeal',{nick:'Petya',text:'я больше не буду'});
  check(r.code===200, 'апелляция принята');

  // разбан
  r = await call('POST','/admin/unban',{token:admin,nick:'Petya'});
  r = await call('GET','/status?nick=Petya');
  check(r.json.banned===false, 'после разбана чист');

  // предупреждение
  r = await call('POST','/admin/warn',{token:admin,nick:'Petya',text:'последнее китайское'});
  r = await call('GET','/status?nick=Petya');
  check(r.json.warnings.length===1 && r.json.warnings[0].text==='последнее китайское', 'предупреждение дошло');

  // запрет «Вместе»
  r = await call('POST','/admin/together',{token:admin,nick:'Petya',on:true});
  r = await call('GET','/status?nick=Petya');
  check(r.json.blockedTogether===true, 'Вместе запрещено');
  r = await call('POST','/admin/together',{token:admin,nick:'Petya',on:false});
  r = await call('GET','/status?nick=Petya');
  check(r.json.blockedTogether===false, 'Вместе снова разрешено');

  // блок мода
  r = await call('POST','/admin/blockmod',{token:admin,id:'xray',version:'*'});
  check(r.code===200 && r.json.mods.some(m=>m.id==='xray'), 'мод xray заблокирован для всех версий');
  r = await call('POST','/admin/blockmod',{token:admin,id:'jei',version:'1.21.4'});
  r = await call('GET','/status?nick=Petya');
  check(r.json.blockedMods.length===2, 'заблокировано модов: '+r.json.blockedMods.length);

  // обновление + changelog
  r = await call('POST','/admin/update',{token:admin,version:'1.18',changelog:'починили туннель',critical:true});
  r = await call('GET','/status?nick=Petya');
  check(r.json.update && r.json.update.version==='1.18' && r.json.update.critical===true, 'экстренное обновление объявлено');
  check(r.json.update.changelog==='починили туннель', 'changelog на месте');

  // команда серверу
  r = await call('POST','/admin/command',{token:admin,text:'say всем привет'});
  check(r.code===200, 'команда поставлена в очередь');

  // сводка админа
  r = await call('POST','/admin/state',{token:admin});
  check(r.code===200 && r.json.accounts.length>=2, 'сводка: аккаунтов '+r.json.accounts.length);
  check(r.json.appeals.some(a=>a.nick==='Petya'), 'апелляция видна админу');
  check(r.json.commands.length>=1, 'команда видна в сводке');

  // просроченный бан снимается сам
  r = await call('POST','/admin/ban',{token:admin,nick:'Vasya',minutes:-1,reason:'тест'}); // отрицательное -> навсегда? minutes>0 иначе 0
  // проверим авто-снятие коротким баном:
  // забанить на 0.0001 мин нельзя (целые), поэтому эмулируем: minutes=1 и подождать нельзя.
  // вместо этого убедимся, что не-существующий ник чист
  r = await call('GET','/status?nick=Nobody');
  check(r.json.banned===false, 'незнакомый ник не забанен');

  console.log(fails===0?'\nВСЁ СОШЛОСЬ':'\nОШИБОК: '+fails);
  process.exit(fails===0?0:1);
})();
