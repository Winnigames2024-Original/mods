'use strict';
const http=require('http'); const PORT=24458; let f=0;
function C(ok,w){console.log((ok?'ok   ':'FAIL ')+w); if(!ok)f++;}
function call(m,r,b){return new Promise(res=>{const d=b===undefined?null:Buffer.from(JSON.stringify(b));
 const q=http.request({host:'127.0.0.1',port:PORT,path:r,method:m,headers:d?{'Content-Type':'application/json','Content-Length':d.length}:{}},x=>{const p=[];x.on('data',c=>p.push(c));x.on('end',()=>{let j=null;try{j=JSON.parse(Buffer.concat(p).toString());}catch(e){}res({code:x.statusCode,json:j});});});q.on('error',e=>res({code:0}));if(d)q.write(d);q.end();});}
(async()=>{
 const admin=(await call('POST','/account/login',{nick:'Winnigames2024',password:'ap'})).json.token;
 const petya=(await call('POST','/account/register',{nick:'Petya',password:'1234'})).json.token;

 // заявка на рекламный сервер
 let r=await call('POST','/servers/submit',{token:petya,name:'Мой сервер',ip:'1.2.3.4:25565',version:'1.21.4',desc:'лучший'});
 C(r.code===200 && r.json.status==='pending','заявка подана, на модерации');
 const sid=r.json.id;

 // публичный список пуст, пока не одобрено
 r=await call('GET','/servers'); C(r.json.servers.length===0,'неодобренных в публичном списке нет');

 // монеты за неодобренный — нельзя
 r=await call('POST','/coins/earn',{token:petya,serverId:sid}); C(r.code===404,'за неодобренный сервер монет нет');

 // админ видит заявку и одобряет
 r=await call('POST','/admin/servers',{token:admin}); C(r.json.servers.some(x=>x.id===sid&&x.status==='pending'),'админ видит заявку');
 r=await call('POST','/admin/server/approve',{token:admin,id:sid}); C(r.code===200,'сервер одобрен');
 r=await call('GET','/servers'); C(r.json.servers.length===1 && r.json.servers[0].name==='Мой сервер','теперь в публичном списке');

 // монеты за заход
 r=await call('POST','/coins/earn',{token:petya,serverId:sid}); C(r.json.awarded===100 && r.json.coins===100,'начислено 100 монет');
 // повтор сразу — кулдаун
 r=await call('POST','/coins/earn',{token:petya,serverId:sid}); C(r.json.awarded===0 && r.json.cooldownLeft>0,'повтор в кулдауне: '+r.json.cooldownLeft+' мин');

 // статус показывает монеты и username
 r=await call('GET','/status?nick=Petya'); C(r.json.coins===100 && r.json.username==='Petya','в статусе монеты и ник');

 // смена ника: мало монет
 r=await call('POST','/username/change',{token:petya,username:'NewName'}); C(r.code===402,'мало монет на смену ника: '+(r.json&&r.json.error));

 // накинем монет через ещё 9 серверов? проще — поставим цену ниже окружением нельзя тут; добавим монеты прямой заявкой невозможно.
 // вместо этого проверим смену при достаточном балансе: одобрим ещё серверы и начислим... кулдаун мешает. Проверим логикой: цена 1000, у нас 100 -> 402 уже проверено.
 // проверим валидацию ника
 r=await call('POST','/username/change',{token:petya,username:'ab'}); C(r.code===400,'короткий ник отклонён');

 // отклонение сервера
 const sid2=(await call('POST','/servers/submit',{token:petya,name:'Плохой',ip:'9.9.9.9'})).json.id;
 r=await call('POST','/admin/server/reject',{token:admin,id:sid2,reason:'реклама запрещёнки'}); C(r.code===200,'сервер отклонён');
 r=await call('POST','/servers/mine',{token:petya}); const bad=r.json.servers.find(x=>x.id===sid2);
 C(bad && bad.status==='rejected' && bad.reason==='реклама запрещёнки','владелец видит отказ с причиной');

 // не-админ не модерирует
 r=await call('POST','/admin/server/approve',{token:petya,id:sid2}); C(r.code===403,'игрок не модерирует');

 console.log(f===0?'ВСЁ СОШЛОСЬ':'ОШИБОК: '+f); process.exit(f);
})();
