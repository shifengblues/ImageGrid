import { decode, convertIndexedToRgb, encode } from 'fast-png';
import type { Region } from './geometry.ts';
export interface Pixels {width:number;height:number;data:Uint8Array}
export function pngPixels(bytes:ArrayBuffer):Pixels {
  const png=decode(bytes,{checkCrc:true});
  if(png.depth===8&&png.channels===4&&!png.palette)return {width:png.width,height:png.height,data:new Uint8Array(png.data.buffer,png.data.byteOffset,png.data.byteLength)};
  if(png.palette){
    const data=convertIndexedToRgb(png),channels=png.palette[0].length;
    if(channels===4)return {width:png.width,height:png.height,data};
    const rgba=new Uint8Array(png.width*png.height*4);
    for(let i=0;i<png.width*png.height;i++)rgba.set([data[i*3],data[i*3+1],data[i*3+2],255],i*4);
    return {width:png.width,height:png.height,data:rgba};
  }
  const data=new Uint8Array(png.width*png.height*4),max=2**png.depth-1;
  for(let y=0;y<png.height;y++)for(let x=0;x<png.width;x++){
    const i=y*png.width+x;
    const raw=png.depth<8?[(png.data[y*Math.ceil(png.width*png.depth/8)+Math.floor(x*png.depth/8)]>>(8-png.depth-x*png.depth%8))&max]:Array.from(png.data.subarray(i*png.channels,(i+1)*png.channels));
    const values=raw.map(v=>Math.round(v*255/max));
    const gray=png.channels<3;
    const alpha=png.channels===2?values[1]:png.channels===4?values[3]:png.transparency&&raw.every((v,k)=>v===png.transparency![k])?0:255;
    data.set([values[0],gray?values[0]:values[1],gray?values[0]:values[2],alpha],i*4);
  }
  return {width:png.width,height:png.height,data};
}
export function copyRegion(source:Pixels,region:Region):Pixels {
  const data=new Uint8Array(region.width*region.height*4);
  for(let row=0;row<region.height;row++){
    const start=((region.y+row)*source.width+region.x)*4;
    data.set(source.data.subarray(start,start+region.width*4),row*region.width*4);
  }
  return {...region,data};
}
export function encodePixels(pixels:Pixels){return encode({...pixels,channels:4,depth:8},{zlib:{level:6}});}
export function rgb(hex:string):[number,number,number,number]{
  if(!/^#[0-9a-f]{6}$/i.test(hex))throw new Error('颜色请输入 #RRGGBB，例如 #FFFFFF。');
  return [parseInt(hex.slice(1,3),16),parseInt(hex.slice(3,5),16),parseInt(hex.slice(5,7),16),255];
}
export function flatBackground(pixels:Pixels,hex:string){
  const bg=rgb(hex);
  for(let i=0;i<pixels.data.length;i+=4){const a=pixels.data[i+3];for(let c=0;c<3;c++)pixels.data[i+c]=Math.round((pixels.data[i+c]*a+bg[c]*(255-a))/255);pixels.data[i+3]=255;}
}
