import {processJob} from './engine.ts';
import type {Job} from './protocol.ts';
self.onmessage=async(event:MessageEvent<Job>)=>{
  const progress=(value:number)=>postMessage({type:'progress',value});
  try{const result=await processJob(event.data,progress);progress(1);postMessage({type:'result',result});}
  catch(error){postMessage({type:'error',message:error instanceof Error?error.message:'图片处理失败，请重试。'});}
};
