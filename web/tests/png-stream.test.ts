import {test} from 'node:test';
import assert from 'node:assert/strict';
import {pngBlob} from '../src/png-stream.ts';
import {pngPixels} from '../src/pixels.ts';
test('streamed PNG preserves every RGBA byte across multiple compression blocks',async()=>{
  const width=1024,height=140,data=new Uint8Array(width*height*4);
  for(let i=0;i<data.length;i++)data[i]=(i*37+(i>>>9))%256;
  const blob=await pngBlob({width,height,data});const decoded=pngPixels(await blob.arrayBuffer());
  assert.equal(decoded.width,width);assert.equal(decoded.height,height);assert.deepEqual(decoded.data,data);
});
