import type { Grid, Placement } from './geometry.ts';
export type Job=
  |{kind:'inspect';file:File;edge?:number}
  |{kind:'compose';files:File[];grid:Grid;mode:Placement;background:string;gapColor:string;format:'png'|'jpg'}
  |{kind:'split';file:File;xs:number[];ys:number[];inset:number;archive:boolean};
export interface Result {width?:number;height?:number;thumbnail?:Blob;blob?:Blob;name?:string;files?:{name:string;blob:Blob}[]}
