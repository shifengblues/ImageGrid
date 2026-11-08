import { test } from 'node:test';
import assert from 'node:assert/strict';
import { layout, placement, equalCuts, regions, moveItem } from '../src/geometry.ts';

test('automatic cells contain original sizes with exact output gaps', () => {
  const grid=layout([{width:1920,height:1080}],{columns:2,rows:2,ratio:[16,9],gap:3});
  assert.deepEqual(grid,{columns:2,rows:2,cellWidth:1920,cellHeight:1080,gap:3,width:3843,height:2163});
});
test('small 4:3 image fills 16:9 cell, centered without stretching', () => {
  const fill=placement(120,90,320,180,'fill');
  assert.deepEqual(fill,{x:0,y:-30,width:320,height:240});
  assert.deepEqual(placement(120,90,320,180,'original'),{x:100,y:45,width:120,height:90});
  assert.deepEqual(placement(120,90,320,180,'fit'),{x:40,y:0,width:240,height:180});
});
test('invalid inputs and capacity overflow cannot silently drop files', () => {
  assert.throws(()=>layout([{width:100,height:100},{width:100,height:100}],{columns:1,rows:1,ratio:[1,1],gap:0}));
  for(const bad of [NaN,1.5,0,11])assert.throws(()=>layout([],{columns:bad,rows:1,ratio:[16,9],gap:0}));
  assert.throws(()=>layout([],{columns:2,rows:2,ratio:[16,0],gap:0}));
});
test('integer cuts cover all pixels and inset rejects vanished regions',()=>{
  assert.deepEqual(equalCuts(11,3),[0,3,7,11]);
  assert.deepEqual(regions([0,4,11],[0,5,9],1),[{x:1,y:1,width:2,height:3},{x:5,y:1,width:5,height:3},{x:1,y:6,width:2,height:2},{x:5,y:6,width:5,height:2}]);
  assert.throws(()=>regions([0,2],[0,2],1));
  assert.throws(()=>regions([0,8,5],[0,8],0));
});
test('insert-style reorder preserves every image',()=>{
  assert.deepEqual(moveItem(['a','b','c','d'],0,2),['b','c','a','d']);
  assert.deepEqual(moveItem(['a','b','c'],2,0),['c','a','b']);
});
