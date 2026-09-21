// Run against a disposable Development API using Database__UseInMemory=true.
import assert from 'node:assert/strict';
const base=process.argv[2]||'http://127.0.0.1:5095';
if(new URL(base).hostname!=='127.0.0.1')throw Error('This test only writes to a local disposable API.');
async function call(path,token,body,method=body?'POST':'GET',expected=200){
 const response=await fetch(base+path,{method,headers:{'Content-Type':'application/json',...(token?{Authorization:'Bearer '+token}:{})},body:body?JSON.stringify(body):undefined});
 assert.equal(response.status,expected,`${method} ${path}: ${await response.clone().text()}`);
 return response.status===204?null:response.json().catch(()=>null);
}
const login=(username,password,expected=200)=>call('/api/auth/login',null,{username,password},'POST',expected);
const admin=(await login('admin','1234')).accessToken;
const company=(await call('/api/companies',admin)).items[0];
const rep=await call('/api/users',admin,{name:'Rep - Demo',username:'demo_rep',password:'1234',role:'Rep',companyId:company.id,territory:'Area - 01',active:true},'POST',201);
const token=(await login('demo_rep','1234')).accessToken;
const product=await call('/api/products',admin,{companyId:company.id,sku:'TEST-01',barcode:'TEST-01',name:'Product - 01',category:'',sellingPrice:100,costPrice:50,reorderLevel:0,expiryDate:null,openingStock:10,active:true},'POST',201);
const shop=await call('/api/shops',admin,{companyId:company.id,code:'CUSTOMER-TEST',name:'Customer - 01',contactName:'',phone:'',address:'Address - 01',city:'',creditLimit:10000,active:true},'POST',201);
const order=await call('/api/orders',token,{shopId:shop.id,salesRepId:rep.id,orderNumber:'INV-REP-TEST',orderDate:'2026-09-21',deliveryDate:'2026-09-22',deliveryAddress:'Address - 01',notes:'Local integration test',products:[{productId:product.id,quantity:2,freeIssueQuantity:0,unitPrice:100}]},'POST',201);
assert.ok((await call('/api/orders',admin)).some(o=>o.id===order.id));
assert.equal((await call(`/api/products/${product.id}/stock`,token)).currentStock,8);
await call(`/api/orders/${order.id}/payments`,token,{paymentDate:'2026-09-21',paidAmount:50,method:'Cash',reference:'PAY-TEST'});
assert.equal((await call(`/api/orders/${order.id}`,admin)).payments[0].paidAmount,50);
await call(`/api/users/${rep.id}`,admin,{name:rep.name,companyId:company.id,role:'Rep',territory:'',active:false},'PUT');
await call('/api/orders',token,null,'GET',401);await login('demo_rep','1234',401);
await call(`/api/users/${rep.id}`,admin,{name:rep.name,companyId:company.id,role:'Rep',territory:'',active:true},'PUT');
const beforeReset=(await login('demo_rep','1234')).accessToken;
await call(`/api/users/${rep.id}/reset-password`,admin,{newPassword:'DemoReset4321'},'POST',204);
await call('/api/orders',beforeReset,null,'GET',401);await login('demo_rep','1234',401);
assert.ok((await login('demo_rep','DemoReset4321')).accessToken);
await call(`/api/users/${rep.id}/reset-password`,admin,{newPassword:'1234'},'POST',204);
const other=(await login('rep01','1234')).accessToken;
await call(`/api/orders/${order.id}`,other,null,'GET',404);
await call('/api/users',token,null,'GET',401);
console.log('PASS: rep login, order visible to admin, stock, payment, deactivate, reset, revoked sessions, and rep isolation.');
