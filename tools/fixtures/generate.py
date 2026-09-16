"""Generate a short, non-private, lossless black/white + gated-tone recording."""
import argparse
import pathlib
import subprocess

p=argparse.ArgumentParser()
p.add_argument('--ffmpeg', required=True)
p.add_argument('--output', required=True)
a=p.parse_args()
out=pathlib.Path(a.output).resolve()
out.parent.mkdir(parents=True,exist_ok=True)
if out.exists():
    raise SystemExit('Refusing to overwrite a fixture.')
subprocess.run([a.ffmpeg, '-nostdin','-hide_banner','-loglevel','error',
    '-f','lavfi','-i', "color=c=black:s=128x72:r=30:d=8,drawbox=c=white:t=fill:enable='gte(t,2)*lt(t,4)'",
    '-f','lavfi','-i', r'aevalsrc=0.5*sin(2*PI*440*t)*gte(t\,2)*lt(t\,4):s=8000:d=8',
    '-map','0:v:0','-map','1:a:0','-c:v','ffv1','-c:a','pcm_s16le',str(out)],check=True,timeout=30)
print(out)
