import assert from 'node:assert/strict';
import {randomUUID} from 'node:crypto';
const base=process.argv[2]||'http://127.0.0.1:5095';
if(new URL(base).hostname!=='127.0.0.1')throw Error('Use a disposable local API only.');
async function call(path,token,body,method=body?'POST':'GET',headers={}){
 const response=await fetch(base+path,{method,headers:{'Content-Type':'application/json',...(token?{Authorization:'Bearer '+token}:{}),...headers},body:body?JSON.stringify(body):undefined});
 const data=response.status===204?null:await response.json();return {status:response.status,data,headers:response.headers};
}
const admin=(await call('/api/auth/login',null,{username:'admin',password:process.env.TEST_ADMIN_PASSWORD||'1234'})).data.accessToken;
async function newRep(){
 const id=randomUUID(),company=(await call('/api/companies',admin,{code:id,name:'Offline test company',contactName:'',phone:'',address:'',active:true})).data;
 const username='offline-'+id;
 const created=await call('/api/users',admin,{companyId:company.id,name:'Offline test rep',username,password:'OfflineTest123!',role:'Rep',territory:'Test',active:true});assert.equal(created.status,201,JSON.stringify(created.data));
 return (await call('/api/auth/login',null,{username,password:'OfflineTest123!'})).data;
}
const rep=await newRep();
const suffix=randomUUID(),companyId=rep.user.companyId;
const product=(await call('/api/products',admin,{companyId,sku:suffix,barcode:suffix,name:'Offline test',category:'General',sellingPrice:10,costPrice:5,reorderLevel:1,openingStock:100,active:true})).data;
const shop=(await call('/api/shops',admin,{companyId,code:suffix,name:'Offline shop',contactName:'',phone:'',address:'',city:'',creditLimit:1000,active:true})).data;
const snapshot=(await call('/api/offline/snapshot',rep.accessToken)).data;
assert.ok(snapshot.reads['/api/products'].items.some(x=>x.id===product.id));
assert.equal(snapshot.reads['/api/users'],undefined);
assert.ok(!JSON.stringify(snapshot).includes('passwordHash'));
const body={shopId:shop.id,salesRepId:rep.user.id,orderNumber:suffix,orderDate:'2026-09-25',deliveryDate:'2026-09-25',deliveryAddress:'',notes:'',products:[{productId:product.id,quantity:2,freeIssueQuantity:0,unitPrice:10}]};
const headers={'X-Offline-Operation':randomUUID(),'X-Offline-Versions':JSON.stringify({['products/'+product.id]:snapshot.versions['products/'+product.id]})};
const first=await call('/api/orders',rep.accessToken,body,'POST',headers);assert.equal(first.status,201,JSON.stringify(first.data));
const repeats=await Promise.all(Array.from({length:3},()=>call('/api/orders',rep.accessToken,body,'POST',headers)));
for(const repeated of repeats){assert.equal(repeated.status,201);assert.equal(repeated.data.id,first.data.id);}
assert.equal((await call('/api/products/'+product.id+'/stock',admin)).data.currentStock,98);
assert.equal((await call('/api/orders',rep.accessToken,{...body,orderNumber:randomUUID()},'POST',headers)).status,409);
const stale=await call('/api/products/'+product.id+'/adjustments',admin,{quantity:1,direction:'IN',notes:''},'POST',{'X-Offline-Operation':randomUUID(),'X-Offline-Versions':headers['X-Offline-Versions']});assert.equal(stale.status,409);
const payHeaders={'X-Offline-Operation':randomUUID()},payment={paidAmount:5,paymentDate:'2026-09-25',method:'Cash',reference:''};
assert.equal((await call('/api/orders/'+first.data.id+'/payments',rep.accessToken,payment,'POST',payHeaders)).status,200);
assert.equal((await call('/api/orders/'+first.data.id+'/payments',rep.accessToken,payment,'POST',payHeaders)).status,200);
const saved=(await call('/api/orders/'+first.data.id,admin)).data;assert.equal(saved.payments.length,1);
const other=await newRep();
const otherSnapshot=(await call('/api/offline/snapshot',other.accessToken)).data;
assert.ok(!otherSnapshot.reads['/api/orders'].some(x=>x.id===first.data.id));
assert.ok(!otherSnapshot.reads['/api/products'].items.some(x=>x.id===product.id));
console.log('PASS: offline snapshot, role isolation, duplicate/retried/concurrent operations, stale-record conflict, single payment and inventory movement.');
