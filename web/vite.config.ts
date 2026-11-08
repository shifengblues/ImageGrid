import { defineConfig } from 'vite';
export default defineConfig({
  base:'/ImageGrid/',
  build:{target:'es2022'},
  plugins:[{
    name:'local-processing-policy',apply:'build',
    transformIndexHtml(html){return html.replace('<!-- production-csp -->',`<meta http-equiv="Content-Security-Policy" content="default-src 'self'; script-src 'self'; worker-src 'self' blob:; style-src 'self' 'unsafe-inline'; img-src 'self' blob: data:; connect-src 'none'; object-src 'none'; base-uri 'self'; form-action 'none'">`);}
  }]
});
