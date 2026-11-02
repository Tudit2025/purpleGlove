"""Read-only audit of the returned manuscript, live assets and book cipher."""
from pathlib import Path
import ast
import hashlib
import importlib.util
import json
import re

ROOT = Path(__file__).resolve().parents[2]
OUT = Path(__file__).resolve().parent
spec = importlib.util.spec_from_file_location('review', ROOT / 'output/performance-edit/build_review_pack.py')
review = importlib.util.module_from_spec(spec)
spec.loader.exec_module(review)
events = review.load_events()
runtime = review.runtime_choices()
manifest = json.loads((OUT / 'import-manifest.json').read_text(encoding='utf-8'))
checks = 0

def check(value, message):
    global checks
    assert value, message
    checks += 1

for source in manifest['sources']:
    check(hashlib.sha256((OUT / 'source' / source['file']).read_bytes()).hexdigest() == source['sha256'], 'Source snapshot changed')
for event_id, expected in manifest['events'].items():
    actual = events[event_id]['lines']
    check(len(actual) == len(expected), f'Wrong line count: {event_id}')
    for index, (old, new) in enumerate(zip(expected, actual)):
        check(old['speaker'] == new['speaker'] and old['text'] == new['text'], f'Line mismatch: {event_id}/{index}')
        check(old.get('choices', []) == new['choices'], f'Choices mismatch: {event_id}/{index}')
        check(old.get('endEvent', False) == new['endEvent'], f'Terminal marker mismatch: {event_id}/{index}')
for event_id, expected in manifest['runtime'].items():
    check(expected[0]['text'] == runtime[event_id]['lines'][0]['text'], f'Runtime prompt mismatch: {event_id}')
    check(expected[0]['choices'] == runtime[event_id]['lines'][0]['choices'], f'Runtime options mismatch: {event_id}')

text = (ROOT / 'Assets/Scripts/Presentation/BookCipherText.cs').read_text(encoding='utf-8')
cipher = ast.literal_eval(re.search(r'public const string Cipher = (".*");', text)[1])
flower = re.findall(r'"([^"\n]+)"', text.split('string[] Flower = {')[1].split('};')[0])
ordinary = re.findall(r'"([^"\n]+)"', text.split('string[] Ordinary = {')[1].split('};')[0])
coords = [tuple(map(int, coord.split('-'))) for coord in re.findall(r'\d+-\d+-\d+', cipher)]
def decode(lines):
    lines = [re.sub('[^A-Z]', '', line) for line in lines]
    return ''.join(lines[(p-12)*4+l-1][c-1] for p,l,c in coords)
check(len(coords) == 21 and len(cipher.splitlines()) == 5, 'Wrong coordinate layout')
check(decode(flower) == 'REQUESTCASEFIFTYSEVEN', 'Wrong flower edition answer')
check(decode(ordinary) != decode(flower), 'Wrong edition must not solve the cipher')
scene = (ROOT / 'Assets/Art/DeskSceneV1/Scenes/PH_DeskRoom.unity').read_text(encoding='utf-8')
check('answer: REQUEST CASE FIFTY SEVEN' in scene and json.dumps(cipher) in scene, 'Serialized scene mismatch')
for root in ('Assets/Scripts', 'Assets/Art/DeskSceneV1/Editor', 'Assets/Art/DeskSceneV1/Scenes', 'Assets/Data/Dialogue'):
    for path in (ROOT / root).rglob('*'):
        if path.suffix not in ('.cs', '.asset', '.unity'): continue
        body = path.read_text(encoding='utf-8')
        # Unity may encode scalars as Unicode escape sequences.
        body = re.sub(r'\\u([0-9a-fA-F]{4})', lambda m: chr(int(m[1], 16)), body)
        check(not any(stale in body for stale in ('第七号', '7—031', '7-031', '七—〇三一', 'REQUEST CASE SEVEN')), f'Stale identifier: {path}')

indexed = [stage[3] for stages in review.STAGES for stage in stages] + [entry[1] for entry in review.COMMON] + review.LEGACY
check(len(set(indexed)) == len(indexed) and set(indexed) == set(events) | set(runtime), 'Review index coverage differs from assets')
result = {'status': 'PASS', 'checks': checks, 'imported_events': len(manifest['events']),
          'imported_lines': sum(map(len, manifest['events'].values())), 'runtime_choices': len(runtime),
          'archive': 57, 'batch': 726, 'book_answer': decode(flower), 'coordinates': len(coords)}
(OUT / 'text-validation.json').write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf-8')
print(json.dumps(result, ensure_ascii=False))
