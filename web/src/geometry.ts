export interface Size { width:number; height:number }
export interface Grid extends Size { columns:number; rows:number; cellWidth:number; cellHeight:number; gap:number }
export interface Settings { columns:number; rows:number; ratio:[number,number]; gap:number; target?:Size }
export type Placement='original'|'fit'|'fill';
export interface Region extends Size { x:number; y:number }
export function integer(value:number,min:number,max:number,label:string){
  if(!Number.isInteger(value)||value<min||value>max)throw new Error(`${label}须为 ${min}–${max} 的整数。`);
  return value;
}
export function layout(images:Size[],s:Settings):Grid {
  const {columns,rows,gap,ratio:[rw,rh]}=s;
  integer(columns,1,10,'列数');integer(rows,1,10,'行数');integer(gap,0,100,'间距');
  integer(rw,1,10000,'比例');integer(rh,1,10000,'比例');
  if(images.length>columns*rows)throw new Error(`共 ${images.length} 张图片，当前仅有 ${columns*rows} 个格子，请增加行列或删除图片。`);
  const gcd=(a:number,b:number):number=>b?gcd(b,a%b):a;
  const divisor=gcd(rw,rh),w=rw/divisor,h=rh/divisor;
  const factor=Math.max(1,...images.map(p=>Math.max(Math.ceil(p.width/w),Math.ceil(p.height/h))));
  const cellWidth=s.target?.width??w*factor,cellHeight=s.target?.height??h*factor;
  integer(cellWidth,1,65535,'单格宽度');integer(cellHeight,1,65535,'单格高度');
  return {columns,rows,cellWidth,cellHeight,gap,width:columns*cellWidth+(columns-1)*gap,height:rows*cellHeight+(rows-1)*gap};
}
export function placement(w:number,h:number,cw:number,ch:number,mode:Placement):Region {
  const scale=mode==='original'?1:mode==='fill'?Math.max(cw/w,ch/h):Math.min(cw/w,ch/h);
  const width=w*scale,height=h*scale;
  return {x:mode==='original'?Math.floor((cw-width)/2):(cw-width)/2,y:mode==='original'?Math.floor((ch-height)/2):(ch-height)/2,width,height};
}
export function equalCuts(length:number,count:number){
  integer(count,1,10,'行列');if(length<count)throw new Error('图片像素尺寸不足以按此行列拆分。');
  return Array.from({length:count+1},(_,i)=>Math.floor(length*i/count));
}
export function regions(xs:number[],ys:number[],inset:number):Region[] {
  integer(inset,0,32767,'内缩');const output:Region[]=[];
  if(xs.length<2||ys.length<2)throw new Error('缺少分割线。');
  for(let row=0;row<ys.length-1;row++)for(let col=0;col<xs.length-1;col++){
    const width=xs[col+1]-xs[col]-inset*2,height=ys[row+1]-ys[row]-inset*2;
    if(![xs[col],xs[col+1],ys[row],ys[row+1],width,height].every(Number.isInteger)||width<1||height<1)throw new Error('内缩过大或分割线交叉，每格至少保留 1×1 像素。');
    output.push({x:xs[col]+inset,y:ys[row]+inset,width,height});
  }
  return output;
}
export function moveItem<T>(items:T[],from:number,to:number){
  const result=[...items];if(from<0||from>=items.length||to<0||to>=items.length)return result;
  result.splice(to,0,result.splice(from,1)[0]);return result;
}
