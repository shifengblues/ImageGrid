import {test} from 'node:test';
import assert from 'node:assert/strict';
import {encode} from 'fast-png';
import {pngPixels,encodePixels,copyRegion,flatBackground} from '../src/pixels.ts';
test('PNG roundtrip preserves translucent and fully transparent RGB',()=>{
  const source={width:2,height:2,data:new Uint8Array([77,91,23,0,80,120,160,127,200,150,100,255,1,2,3,255])};
  const encoded=encodePixels(source);const actual=pngPixels(encoded.buffer as ArrayBuffer);
  assert.deepEqual(actual,source);
  assert.deepEqual(copyRegion(actual,{x:1,y:0,width:1,height:2}).data,new Uint8Array([80,120,160,127,1,2,3,255]));
});
test('palette PNG and grayscale transparency decode correctly',()=>{
  const indexed=encode({width:2,height:1,data:new Uint8Array([0,1]),channels:1,depth:8,palette:[[45,67,89,0],[12,34,56,127]]});
  assert.deepEqual(pngPixels(indexed.buffer as ArrayBuffer).data,new Uint8Array([45,67,89,0,12,34,56,127]));
  // Pillow-generated grayscale PNG with a tRNS chunk; fast-png's encoder omits this chunk.
  const gray=Uint8Array.from(Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAIAAAABCAAAAADRSSBWAAAAAnRSTlMADH8lgRMAAAALSURBVHicY+RRBgAAQQAx4laVugAAAABJRU5ErkJggg==','base64'));
  assert.deepEqual(pngPixels(gray.buffer as ArrayBuffer).data,new Uint8Array([12,12,12,0,47,47,47,255]));
});
test('JPEG flatten uses canvas background, including partial alpha',()=>{
  const pixels={width:2,height:1,data:new Uint8Array([100,150,200,0,100,150,200,127])};flatBackground(pixels,'#FFFFFF');
  assert.deepEqual(pixels.data,new Uint8Array([255,255,255,255,178,203,228,255]));
});
