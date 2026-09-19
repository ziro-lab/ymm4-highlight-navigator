"""Generated W1-R review sequence; uses only the explicitly supplied host FFmpeg."""
import argparse
import subprocess
from pathlib import Path

parser = argparse.ArgumentParser()
parser.add_argument('--ffmpeg', required=True)
parser.add_argument('--output', required=True)
args = parser.parse_args()
output = Path(args.output).resolve()
output.parent.mkdir(parents=True, exist_ok=True)
filters = ','.join(f"drawbox=color=white:t=fill:enable='gte(t,{t})*lt(t,{t+1})'" for t in (2, 6, 10, 14, 18, 22))
subprocess.run([args.ffmpeg, '-nostdin', '-v', 'error', '-y', '-f', 'lavfi', '-i',
                'color=c=black:s=128x72:r=30:d=24', '-vf', filters, '-an', '-c:v', 'ffv1', str(output)], check=True, timeout=30)
print(output)
