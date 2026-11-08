'use strict';
const http=require('http'); const PORT=24456; let f=0;
function C(ok,w){console.log((ok?'ok   ':'FAIL ')+w); if(!ok)f++;}
function call(m,r,b){return new Promise(res=>{const d=b===undefined?null:Buffer.from(JSON.stringify(b));
 const q=http.request({host:'127.0.0.1',port:PORT,path:r,method:m,headers:d?{'Content-Type':'application/json','Content-Length':d.length}:{}},x=>{const p=[];x.on('data',c=>p.push(c));x.on('end',()=>{let j=null;try{j=JSON.parse(Buffer.concat(p).toString());}catch(e){}res({code:x.statusCode,json:j});});});q.on('error',e=>res({code:0}));if(d)q.write(d);q.end();});}
(async()=>{
 const a=(await call('POST','/account/login',{nick:'Winnigames2024',password:'ap'})).json.token;
 await call('POST','/admin/command',{token:a,text:'say привет'});
 await call('POST','/admin/command',{token:a,text:'time set day'});
 let r=await call('GET','/commands?key=wrong'); C(r.code===403,'без ключа отказ');
 r=await call('GET','/commands?key=secretkey'); C(r.code===200 && r.json.commands.length===2,'две команды выданы: '+(r.json.commands||[]).join(', '));
 r=await call('GET','/commands?key=secretkey'); C(r.json.commands.length===0,'после выдачи очередь пуста');
 console.log(f===0?'ВСЁ СОШЛОСЬ':'ОШИБОК: '+f); process.exit(f);
})();
