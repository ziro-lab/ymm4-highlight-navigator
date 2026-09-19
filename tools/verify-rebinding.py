"""Independent W1-R requirement and exact-checkout gate; producers cannot self-certify."""
import base64
import hashlib
import json
import os
from pathlib import Path

required = json.loads(Path('tests/Ymm4HighlightNavigator.Rebinding.Tests/required-cases.json').read_text())
assert len(required) == 29 and len(required) == len(set(required)), 'Invalid requirement contract'
result = json.loads(Path('out/rebinding-tests/results.json').read_text())
assert result['schema'] == 'navigator.rebinding-tests.v1' and result['checkout'] == os.environ['GITHUB_SHA']
ids = [case['id'] for case in result['cases']]
assert len(ids) == len(set(ids)) and set(ids) == set(required), 'Missing or duplicate test'
assert result['failures'] == 0 and all(case['passed'] is True for case in result['cases'])
print('Independent rebinding gate PASS:', len(ids), 'cases;', result['assertions'], 'assertions')

learning_required = json.loads(Path('tests/Ymm4HighlightNavigator.Learning.Tests/required-cases.json').read_text())
assert len(learning_required) == 35 and len(set(learning_required)) == 35
for folder in ('learning-tests', 'learning-trim-tests'):
    learning = json.loads(Path(f'out/{folder}/results.json').read_text())
    ids = [case['id'] for case in learning['cases']]
    assert learning['schema'] == 'navigator.learning-tests.v1' and learning['checkout'] == os.environ['GITHUB_SHA']
    assert len(ids) == len(set(ids)) and set(ids) == set(learning_required)
    assert learning['failures'] == 0 and all(case['passed'] is True for case in learning['cases'])
envelope = json.loads(Path('out/learning-trim-tests/real-media-batch-dedupe-raw-free/corpus/corpus.json').read_text())
payload = base64.b64decode(envelope['payload'])
assert hashlib.sha256(payload).hexdigest() == envelope['sha256']
samples = json.loads(payload)['samples']
assert len(samples) == 1 and abs(samples[0]['endSeconds'] - 6.0) < 0.001
meta = json.loads(Path('out/trim-metadata.json').read_text())
assert float(meta['format']['duration']) >= 6.0
print('Independent learning gate PASS: 35 cases x ordinary/stream-copy media; video domain 6.0s')
