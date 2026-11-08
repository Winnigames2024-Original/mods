'use strict';const http=require('http');const PORT=24460;let f=0;
function C(o,w){console.log((o?'ok   ':'FAIL ')+w);if(!o)f++;}
function call(m,r,b){return new Promise(res=>{const d=b===undefined?null:Buffer.from(JSON.stringify(b));const q=http.request({host:'127.0.0.1',port:PORT,path:r,method:m,headers:d?{'Content-Type':'application/json','Content-Length':d.length}:{}},x=>{const p=[];x.on('data',c=>p.push(c));x.on('end',()=>{let j=null;try{j=JSON.parse(Buffer.concat(p).toString());}catch(e){}res({code:x.statusCode,json:j});});});q.on('error',e=>res({code:0}));if(d)q.write(d);q.end();});}
(async()=>{
 const a=(await call('POST','/account/login',{nick:'Winnigames2024',password:'ap'})).json.token;
 await call('POST','/account/register',{nick:'Petya',password:'1234'});
 await call('POST','/admin/ban',{token:a,nick:'Petya',minutes:30,reason:'x'});
 let r=await call('POST','/admin/state',{token:a});
 const p=r.json.accounts.find(x=>x.nick==='Petya');
 C(p && p.banned===true,'Petya в списке и забанен');
 C(p.coins===0 && p.username==='Petya','монеты и ник в списке');
 const adm=r.json.accounts.find(x=>x.nick==='Winnigames2024');
 C(adm && adm.admin===true,'админ помечен');
 console.log(f===0?'ВСЁ СОШЛОСЬ':'ОШИБОК: '+f);process.exit(f);
})();
