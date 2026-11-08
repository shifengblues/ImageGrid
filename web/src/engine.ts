import {zipSync} from 'fflate';
import {hasPngSignature} from 'fast-png';
import {placement,regions} from './geometry.ts';
import type {Size} from './geometry.ts';
import {pngPixels,encodePixels,copyRegion,rgb,flatBackground} from './pixels.ts';
import type {Pixels} from './pixels.ts';
import type {Job,Result} from './protocol.ts';
import {pngBlob} from './png-stream.ts';

function guard(size:Size){
  if(!Number.isInteger(size.width)||!Number.isInteger(size.height)||size.width<1||size.height<1||size.width>0x7fffffff||size.height>0x7fffffff||!Number.isSafeInteger(size.width*size.height*4))throw new Error('图像尺寸无效或超过图片格式可表示的范围。');
}
async function pngHeader(file:File){
  const bytes=await file.slice(0,32).arrayBuffer();
  if(!hasPngSignature(new Uint8Array(bytes)))return null;
  const view=new DataView(bytes);const size={width:view.getUint32(16),height:view.getUint32(20)};
  guard(size);return size;
}
async function bitmap(file:File){
  await pngHeader(file);
  const image=await createImageBitmap(file);
  try{guard(image);return image;}catch(error){image.close();throw error;}
}
async function readPixels(file:File):Promise<Pixels>{
  if(await pngHeader(file))return pngPixels(await file.arrayBuffer());
  const image=await bitmap(file);
  let canvas:ReturnType<typeof surface>|undefined;
  try{
    canvas=surface(image.width,image.height);
    canvas.ctx.drawImage(image,0,0);const result=canvas.ctx.getImageData(0,0,image.width,image.height);
    return {width:image.width,height:image.height,data:new Uint8Array(result.data.buffer)};
  }finally{image.close();canvas?.release();}
}
function fill(pixels:Pixels,x:number,y:number,w:number,h:number,color:string){
  const row=new Uint8Array(w*4),rgba=rgb(color);
  for(let i=0;i<row.length;i+=4)row.set(rgba,i);
  for(let r=y;r<y+h;r++)pixels.data.set(row,(r*pixels.width+x)*4);
}
function surface(width:number,height:number){
  guard({width,height});
  const canvas=typeof OffscreenCanvas!=='undefined'?new OffscreenCanvas(width,height):Object.assign(document.createElement('canvas'),{width,height});
  const ctx=canvas.getContext('2d',{willReadFrequently:true}) as CanvasRenderingContext2D|OffscreenCanvasRenderingContext2D|null;
  if(!ctx)throw new Error('浏览器无法创建此尺寸的画布，请缩小输出尺寸后重试。');
  try{
    ctx.fillStyle='#010203';ctx.fillRect(width-1,height-1,1,1);
    const pixel=ctx.getImageData(width-1,height-1,1,1).data;
    if(pixel[0]!==1||pixel[1]!==2||pixel[2]!==3)throw new Error('当前浏览器无法创建此尺寸的画布，请尝试其他浏览器或较小输出尺寸。');
    ctx.clearRect(width-1,height-1,1,1);
  }catch(error){canvas.width=canvas.height=1;throw error;}
  return {canvas,ctx,release:()=>{canvas.width=canvas.height=1;},blob:(type:string,quality=1):Promise<Blob>=>'convertToBlob' in canvas?canvas.convertToBlob({type,quality}):new Promise((resolve,reject)=>canvas.toBlob(blob=>blob?resolve(blob):reject(new Error('浏览器无法导出此尺寸的图片。')),type,quality))};
}
export async function processJob(job:Job,progress:(value:number)=>void):Promise<Result>{
  if(typeof createImageBitmap==='undefined')throw new Error('此浏览器版本不支持本地图像处理，请升级浏览器后重试。');
  if(job.kind==='inspect'){
    const image=await bitmap(job.file);
    let result:ReturnType<typeof surface>|undefined;
    try{
      const scale=Math.min(1,(job.edge??640)/Math.max(image.width,image.height));
      result=surface(Math.max(1,Math.round(image.width*scale)),Math.max(1,Math.round(image.height*scale)));
      result.ctx.drawImage(image,0,0,result.canvas.width,result.canvas.height);
      return {width:image.width,height:image.height,thumbnail:await result.blob('image/png')};
    }finally{image.close();result?.release();}
  }
  if(job.kind==='split'){
    const areas=regions(job.xs,job.ys,job.inset),width=job.xs.at(-1)!,height=job.ys.at(-1)!;
    guard({width,height});
    const source=await readPixels(job.file);
    if(source.width!==width||source.height!==height)throw new Error('源图尺寸发生变化，请重新导入。');
    const entries:{name:string;blob:Blob}[]=[],archive:Record<string,Uint8Array>={};
    const stem=job.file.name.replace(/\.[^.]+$/,'').replace(/[\\/:*?"<>|\x00-\x1f]/g,'_').slice(0,100)||'Image';
    for(let i=0;i<areas.length;i++){
      const pixels=copyRegion(source,areas[i]),encoded=encodePixels(pixels),name=`${stem}_${String(i+1).padStart(2,'0')}.png`;
      if(job.archive)archive[name]=encoded;else entries.push({name,blob:new Blob([encoded as BlobPart],{type:'image/png'})});
      progress((i+1)/(areas.length+1));
    }
    if(!job.archive)return {files:entries};
    return {blob:new Blob([zipSync(archive,{level:0}) as BlobPart],{type:'application/zip'}),name:`${stem}_split.zip`};
  }
  const {grid,files}=job;
  if(files.length>grid.columns*grid.rows)throw new Error('图片数量超过宫格容量。');
  guard(grid);
  let output:Pixels|undefined,canvas:ReturnType<typeof surface>|undefined;
  try{
  if(job.mode==='original'){
    output={width:grid.width,height:grid.height,data:new Uint8Array(grid.width*grid.height*4)};
    fill(output,0,0,grid.width,grid.height,job.background);
    for(let c=1;c<grid.columns;c++)fill(output,c*grid.cellWidth+(c-1)*grid.gap,0,grid.gap,grid.height,job.gapColor);
    for(let r=1;r<grid.rows;r++)fill(output,0,r*grid.cellHeight+(r-1)*grid.gap,grid.width,grid.gap,job.gapColor);
  }else{
    canvas=surface(grid.width,grid.height);const ctx=canvas.ctx;
    ctx.fillStyle=job.gapColor;ctx.fillRect(0,0,grid.width,grid.height);
    ctx.fillStyle=job.background;
    for(let i=0;i<grid.columns*grid.rows;i++)ctx.fillRect(i%grid.columns*(grid.cellWidth+grid.gap),Math.floor(i/grid.columns)*(grid.cellHeight+grid.gap),grid.cellWidth,grid.cellHeight);
  }
  for(let i=0;i<files.length;i++){
    const x=i%grid.columns*(grid.cellWidth+grid.gap),y=Math.floor(i/grid.columns)*(grid.cellHeight+grid.gap);
    if(output){
      const source=await readPixels(files[i]);
      if(source.width>grid.cellWidth||source.height>grid.cellHeight)throw new Error('原图无法放入单格，请重新导入。');
      const pos=placement(source.width,source.height,grid.cellWidth,grid.cellHeight,'original');
      for(let row=0;row<source.height;row++)output.data.set(source.data.subarray(row*source.width*4,(row+1)*source.width*4),((y+pos.y+row)*grid.width+x+pos.x)*4);
    }else{
      const image=await bitmap(files[i]);
      try{
        const pos=placement(image.width,image.height,grid.cellWidth,grid.cellHeight,job.mode),ctx=canvas!.ctx;
        ctx.save();ctx.beginPath();ctx.rect(x,y,grid.cellWidth,grid.cellHeight);ctx.clip();ctx.imageSmoothingQuality='high';
        ctx.drawImage(image,x+pos.x,y+pos.y,pos.width,pos.height);ctx.restore();
      }finally{image.close();}
    }
    progress((i+1)/(files.length+1));
  }
  if(output&&job.format==='png')return {blob:await pngBlob(output,value=>progress((files.length+value)/(files.length+1))),name:'Image_Grid.png'};
  if(output){
    flatBackground(output,job.background);canvas=surface(grid.width,grid.height);
    canvas.ctx.putImageData(new ImageData(new Uint8ClampedArray(output.data.buffer as ArrayBuffer),grid.width,grid.height),0,0);
    output=undefined;
  }
  if(job.format==='jpg'){
    const ctx=canvas!.ctx;ctx.globalCompositeOperation='destination-over';ctx.fillStyle=job.background;ctx.fillRect(0,0,grid.width,grid.height);
  }
  return {blob:await canvas!.blob(job.format==='jpg'?'image/jpeg':'image/png'),name:`Image_Grid.${job.format}`};
  }finally{canvas?.release();}
}
