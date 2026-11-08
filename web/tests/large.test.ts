import {test} from 'node:test';
import assert from 'node:assert/strict';
import {processJob} from '../src/engine.ts';
import {layout} from '../src/geometry.ts';

test('25 UHD images reach allocation instead of an arbitrary 512MB rejection',async()=>{
  const sizes=Array.from({length:25},()=>({width:3840,height:2160}));
  const grid=layout(sizes,{columns:5,rows:5,ratio:[16,9],gap:3});
  assert.equal(grid.width,19212);assert.equal(grid.height,10812);
  // Stop at the real allocation boundary; do not allocate an 831MB bitmap in a unit test.
  const previousBitmap=globalThis.createImageBitmap,previousCanvas=globalThis.OffscreenCanvas;
  globalThis.createImageBitmap=async()=>{throw new Error('unexpected decode');};
  globalThis.OffscreenCanvas=class{constructor(){throw new Error('allocation reached');}} as typeof OffscreenCanvas;
  try{
    await assert.rejects(processJob({kind:'compose',files:[],grid,mode:'fill',background:'#FFFFFF',gapColor:'#FFFFFF',format:'png'},()=>{}),/allocation reached/);
  }finally{globalThis.createImageBitmap=previousBitmap;globalThis.OffscreenCanvas=previousCanvas;}
});
