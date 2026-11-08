import {Zlib} from 'fflate';
import type {Pixels} from './pixels.ts';
const crcTable=Uint32Array.from({length:256},(_,n)=>{for(let bit=0;bit<8;bit++)n=n&1?0xedb88320^(n>>>1):n>>>1;return n>>>0;});
function u32(value:number){const bytes=new Uint8Array(4);new DataView(bytes.buffer).setUint32(0,value);return bytes;}
function chunk(type:string,data:Uint8Array):BlobPart[]{
  const name=new TextEncoder().encode(type);let crc=0xffffffff;
  for(const byte of name)crc=crcTable[(crc^byte)&255]^(crc>>>8);
  for(const byte of data)crc=crcTable[(crc^byte)&255]^(crc>>>8);
  return [u32(data.length),name,data as BlobPart,u32((crc^0xffffffff)>>>0)];
}
// Feed scanlines to zlib incrementally: no second full-size filtered image or encoded-array copy.
export async function pngBlob(image:Pixels,progress?:(value:number)=>void):Promise<Blob>{
  const header=new Uint8Array(13);header.set(u32(image.width),0);header.set(u32(image.height),4);header[8]=8;header[9]=6;
  const parts:BlobPart[]=[new Uint8Array([137,80,78,71,13,10,26,10]),...chunk('IHDR',header)];
  const stream=new Zlib({level:6},data=>{if(data.length)parts.push(...chunk('IDAT',data));});
  const rowBytes=image.width*4,rows=Math.max(1,Math.floor(262144/(rowBytes+1))),buffer=new Uint8Array((rowBytes+1)*rows);
  for(let y=0;y<image.height;y+=rows){
    const count=Math.min(rows,image.height-y);
    for(let r=0;r<count;r++){buffer[r*(rowBytes+1)]=0;buffer.set(image.data.subarray((y+r)*rowBytes,(y+r+1)*rowBytes),r*(rowBytes+1)+1);}
    stream.push(buffer.subarray(0,count*(rowBytes+1)),y+count===image.height);
    progress?.((y+count)/image.height);
    if(y%(rows*16)===0)await new Promise<void>(resolve=>setTimeout(resolve,0));
  }
  parts.push(...chunk('IEND',new Uint8Array()));return new Blob(parts,{type:'image/png'});
}
