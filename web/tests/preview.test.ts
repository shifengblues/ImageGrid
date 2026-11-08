import {test} from 'node:test';
import assert from 'node:assert/strict';
import {PreviewCache} from '../src/preview.ts';
test('visible preview loads original 4K detail and releases it when removed',async()=>{
  const file=new File(['test'],'4k.png'),calls:number[]=[];
  const cache=new PreviewCache(async(_file,edge)=>{calls.push(edge);return new Blob(['preview']);},()=>{});
  await cache.request([{file,width:3840,height:2160,edge:3840}]);
  assert.deepEqual(calls,[3840]);assert.equal(cache.get(file)?.edge,3840);
  cache.prune([]);assert.equal(cache.get(file),undefined);
});
test('preview budget evicts offscreen images instead of retaining every original',async()=>{
  const a=new File(['a'],'a.png'),b=new File(['b'],'b.png');
  const cache=new PreviewCache(async()=>new Blob(['x']),()=>{},100);
  await cache.request([{file:a,width:4,height:4,edge:4}]);
  await cache.request([{file:b,width:4,height:4,edge:4}]);
  assert.equal(cache.get(a),undefined);assert.equal(cache.get(b)?.edge,4);
});
