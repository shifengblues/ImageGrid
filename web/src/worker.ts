import {zipSync} from 'fflate';
import {hasPngSignature} from 'fast-png';
import {placement,regions} from './geometry.ts';
import type {Size} from './geometry.ts';
import {pngPixels,encodePixels,copyRegion,rgb,flatBackground} from './pixels.ts';
import type {Pixels} from './pixels.ts';
import type {Job,Result} from './protocol.ts';

const LIMIT=512*1024*1024;
function guard(size:Size,bytes:number){
  if(size.width>16384||size.height>16384||bytes>LIMIT)throw new Error(`当前操作预计需要约 ${Math.ceil(bytes/1048576)} MB 像素缓冲，超出网页版安全范围（512 MB，单边 16384px）。请减少图片、选择统一尺寸，或使用 Windows 版；不会自动降低画质。`);
}
async function pngHeader(file:File){
  const bytes=await file.slice(0,32).arrayBuffer();
  if(!hasPngSignature(new Uint8Array(bytes)))return null;
  const view=new DataView(bytes);const size={width:view.getUint32(16),height:view.getUint32(20)};
  guard(size,size.width*size.height*8);return size;
}
async function bitmap(file:File){
  await pngHeader(file);
  const image=await createImageBitmap(file);
  try{guard(image,image.width*image.height*8);return image;}catch(error){image.close();throw error;}
}
async function readPixels(file:File):Promise<Pixels>{
  if(await pngHeader(file))return pngPixels(await file.arrayBuffer());
  const image=await bitmap(file);
  try{
    const canvas=new OffscreenCanvas(image.width,image.height),context=canvas.getContext('2d',{willReadFrequently:true})!;
    context.drawImage(image,0,0);const result=context.getImageData(0,0,image.width,image.height);
    return {width:image.width,height:image.height,data:new Uint8Array(result.data.buffer)};
  }finally{image.close();}
}
function fill(pixels:Pixels,x:number,y:number,w:number,h:number,color:string){
  const row=new Uint8Array(w*4),rgba=rgb(color);
  for(let i=0;i<row.length;i+=4)row.set(rgba,i);
  for(let r=y;r<y+h;r++)pixels.data.set(row,(r*pixels.width+x)*4);
}
function progress(value:number){postMessage({type:'progress',value});}
async function process(job:Job):Promise<Result>{
  if(typeof OffscreenCanvas==='undefined'||typeof createImageBitmap==='undefined')throw new Error('此浏览器版本不支持本地图像处理，请升级浏览器后重试。');
  if(job.kind==='inspect'){
    if(job.file.size>200*1024*1024)throw new Error('单张文件超过 200 MB，请使用较小文件或 Windows 版。');
    const image=await bitmap(job.file);
    try{
      const scale=Math.min(1,640/Math.max(image.width,image.height));
      const canvas=new OffscreenCanvas(Math.max(1,Math.round(image.width*scale)),Math.max(1,Math.round(image.height*scale)));
      canvas.getContext('2d')!.drawImage(image,0,0,canvas.width,canvas.height);
      return {width:image.width,height:image.height,thumbnail:await canvas.convertToBlob({type:'image/png'})};
    }finally{image.close();}
  }
  if(job.kind==='split'){
    const areas=regions(job.xs,job.ys,job.inset),width=job.xs.at(-1)!,height=job.ys.at(-1)!;
    const largest=Math.max(...areas.map(a=>a.width*a.height));guard({width,height},width*height*12+largest*12);
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
  guard(grid,grid.width*grid.height*12);
  let output:Pixels|undefined,canvas:OffscreenCanvas|undefined;
  if(job.mode==='original'){
    output={width:grid.width,height:grid.height,data:new Uint8Array(grid.width*grid.height*4)};
    fill(output,0,0,grid.width,grid.height,job.background);
    for(let c=1;c<grid.columns;c++)fill(output,c*grid.cellWidth+(c-1)*grid.gap,0,grid.gap,grid.height,job.gapColor);
    for(let r=1;r<grid.rows;r++)fill(output,0,r*grid.cellHeight+(r-1)*grid.gap,grid.width,grid.gap,job.gapColor);
  }else{
    canvas=new OffscreenCanvas(grid.width,grid.height);const ctx=canvas.getContext('2d')!;
    ctx.fillStyle=job.gapColor;ctx.fillRect(0,0,grid.width,grid.height);
    ctx.fillStyle=job.background;
    for(let i=0;i<grid.columns*grid.rows;i++)ctx.fillRect(i%grid.columns*(grid.cellWidth+grid.gap),Math.floor(i/grid.columns)*(grid.cellHeight+grid.gap),grid.cellWidth,grid.cellHeight);
  }
  for(let i=0;i<files.length;i++){
    const x=i%grid.columns*(grid.cellWidth+grid.gap),y=Math.floor(i/grid.columns)*(grid.cellHeight+grid.gap);
    if(output){
      const source=await readPixels(files[i]);guard(grid,grid.width*grid.height*12+source.width*source.height*8);
      if(source.width>grid.cellWidth||source.height>grid.cellHeight)throw new Error('原图无法放入单格，请重新导入。');
      const pos=placement(source.width,source.height,grid.cellWidth,grid.cellHeight,'original');
      for(let row=0;row<source.height;row++)output.data.set(source.data.subarray(row*source.width*4,(row+1)*source.width*4),((y+pos.y+row)*grid.width+x+pos.x)*4);
    }else{
      const image=await bitmap(files[i]);
      try{
        guard(grid,grid.width*grid.height*12+image.width*image.height*8);
        const pos=placement(image.width,image.height,grid.cellWidth,grid.cellHeight,job.mode),ctx=canvas!.getContext('2d')!;
        ctx.save();ctx.beginPath();ctx.rect(x,y,grid.cellWidth,grid.cellHeight);ctx.clip();ctx.imageSmoothingQuality='high';
        ctx.drawImage(image,x+pos.x,y+pos.y,pos.width,pos.height);ctx.restore();
      }finally{image.close();}
    }
    progress((i+1)/(files.length+1));
  }
  if(output&&job.format==='png')return {blob:new Blob([encodePixels(output) as BlobPart],{type:'image/png'}),name:'Image_Grid.png'};
  if(output){
    flatBackground(output,job.background);canvas=new OffscreenCanvas(grid.width,grid.height);
    canvas.getContext('2d')!.putImageData(new ImageData(new Uint8ClampedArray(output.data.buffer as ArrayBuffer),grid.width,grid.height),0,0);
  }
  if(job.format==='jpg'){
    const ctx=canvas!.getContext('2d')!;ctx.globalCompositeOperation='destination-over';ctx.fillStyle=job.background;ctx.fillRect(0,0,grid.width,grid.height);
  }
  return {blob:await canvas!.convertToBlob({type:job.format==='jpg'?'image/jpeg':'image/png',quality:1}),name:`Image_Grid.${job.format}`};
}
self.onmessage=async (event:MessageEvent<Job>)=>{
  try{const result=await process(event.data);progress(1);postMessage({type:'result',result});}
  catch(error){postMessage({type:'error',message:error instanceof Error?error.message:'图片处理失败，请重试。'});}
};
