export interface PreviewRequest {file:File;width:number;height:number;edge:number}
interface Cached {url:string;edge:number;bytes:number}
export class PreviewCache {
  private items=new Map<File,Cached>();private wanted:PreviewRequest[]=[];private version=0;private loading:Promise<void>|null=null;
  private load:(file:File,edge:number)=>Promise<Blob>;private changed:()=>void;private budget:number;
  constructor(load:(file:File,edge:number)=>Promise<Blob>,changed:()=>void,budget=256*1024*1024){this.load=load;this.changed=changed;this.budget=budget;}
  get(file:File){return this.items.get(file);}
  prune(files:File[]){const keep=new Set(files);for(const [file,item]of this.items)if(!keep.has(file)){URL.revokeObjectURL(item.url);this.items.delete(file);}this.wanted=this.wanted.filter(r=>keep.has(r.file));this.version++;}
  request(requests:PreviewRequest[]):Promise<void>{
    this.wanted=requests;this.version++;
    if(!this.loading)this.loading=this.run().finally(()=>{this.loading=null;});
    return this.loading;
  }
  private async run(){
    let observed=-1;
    while(observed!==this.version){
      observed=this.version;const visible=new Set(this.wanted.map(r=>r.file));
      for(const request of [...this.wanted]){
        const {file,width,height,edge}=request,current=this.items.get(file);
        if(current&&current.edge>=edge)continue;
        const ratio=Math.min(1,edge/Math.max(width,height)),expected=Math.ceil(width*ratio)*Math.ceil(height*ratio)*4;
        let used=[...this.items.values()].reduce((sum,item)=>sum+item.bytes,0)-(current?.bytes??0);
        for(const [old,item]of this.items){if(used+expected<=this.budget)break;if(!visible.has(old)){URL.revokeObjectURL(item.url);this.items.delete(old);used-=item.bytes;this.changed();}}
        if(used+expected>this.budget)continue;
        try{
          const blob=await this.load(file,edge);
          if(!this.wanted.some(r=>r.file===file))continue;
          if(used+expected+blob.size>this.budget)continue;
          if(current)URL.revokeObjectURL(current.url);
          this.items.set(file,{url:URL.createObjectURL(blob),edge,bytes:expected+blob.size});this.changed();
        }catch{/* Keep the small preview if a high-resolution decode fails. Export remains independent. */}
      }
    }
  }
}
export function loadPreview(file:File,edge:number):Promise<Blob>{
  if(typeof OffscreenCanvas==='undefined')return import('./engine.ts').then(({processJob})=>processJob({kind:'inspect',file,edge},()=>{})).then(result=>result.thumbnail!);
  return new Promise((resolve,reject)=>{
    const worker=new Worker(new URL('./worker.ts',import.meta.url),{type:'module'});
    worker.onmessage=event=>{if(event.data.type==='result'){worker.terminate();resolve(event.data.result.thumbnail);}else if(event.data.type==='error'){worker.terminate();reject(new Error(event.data.message));}};
    worker.onerror=()=>{worker.terminate();reject(new Error('高清预览加载失败。'));};worker.postMessage({kind:'inspect',file,edge});
  });
}
