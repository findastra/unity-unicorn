"""Synthetic regression tests for face-transfer. Never touches a real project; test runs are not tool usage."""
import importlib.util, json, os, pathlib, tempfile, unittest

HERE = pathlib.Path(__file__).resolve().parent
spec = importlib.util.spec_from_file_location('ft', HERE.parent / 'scripts' / 'face-transfer-20261011.py')
ft = importlib.util.module_from_spec(spec)
spec.loader.exec_module(ft)


def write(root, path, text, guid=None):
    p = pathlib.Path(root, path)
    p.parent.mkdir(parents=True, exist_ok=True)
    p.write_bytes(text if isinstance(text, bytes) else text.encode())
    if guid:
        pathlib.Path(str(p) + '.meta').write_text(f'fileFormatVersion: 2\nguid: {guid}\n')


def fbx(shapes):
    body = b''.join(s.encode() + b'\x00\x01SubDeformer' + b'\x00' * 4 for s in shapes)
    return b'Kaydara FBX Binary  \x00' + body


def anim(path, shapes):
    return '%YAML 1.1\nAnimationClip:\n  m_FloatCurves:\n' + ''.join(
        f'  - curve: {{}}\n    attribute: blendShape.{s}\n    path: {path}\n    classID: 137\n' for s in shapes)


def project(root, shapes):
    write(root, 'Assets/Avatar/face.fbx', fbx(shapes), 'a' * 32)
    write(root, 'Assets/Avatar/params.asset',
          'MonoBehaviour:\n  parameters:\n  - name: FT/v2/JawOpen\n    valueType: 1\n'
          '  - name: FT/v2/EyeLidLeft1\n    valueType: 2\n  - name: Toggle\n    valueType: 2\n', 'b' * 32)


class FaceTransferTest(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.src, self.dst = os.path.join(self.tmp.name, 'src'), os.path.join(self.tmp.name, 'dst')
        project(self.src, ['JawOpen', 'MouthClosed', 'TongueOut'])
        project(self.dst, ['JawOpen', 'MouthClosed'])
        write(self.src, 'Assets/VRCFT Jerry/Anim/jaw.anim', anim('Body', ['JawOpen', 'TongueOut']), 'c' * 32)
        write(self.src, 'Assets/VRCFT Jerry/FX.controller',
              f'AnimatorState:\n  m_Motion: {{fileID: 7400000, guid: {"c" * 32}, type: 2}}\n'
              f'  shared: {{guid: {"d" * 32}}}\n  pkg: {{guid: {"e" * 32}}}\n', 'f' * 32)
        write(self.src, 'Assets/Shared/blend.asset', 'x: 1\n', 'd' * 32)

    def tearDown(self):
        self.tmp.cleanup()

    def test_classify(self):
        self.assertEqual(ft.classify_param('FT/v2/JawOpen4'), ('vrcft-v5-unified', 'JawOpen'))
        self.assertEqual(ft.classify_param('v2/MouthClosed'), ('vrcft-v4-unified', 'MouthClosed'))
        self.assertEqual(ft.classify_param('EyesWiden'), ('vrcft-v3-sranipal', 'EyesWiden'))
        self.assertEqual(ft.classify_param('Toggle'), (None, None))

    def test_scan_and_builds_log(self):
        log = os.path.join(self.tmp.name, 'private', 'builds.jsonl')
        out = os.path.join(self.tmp.name, 'private', 'profile.json')
        p = ft.main(['scan', '--project', self.src, '--out', out, '--builds-log', log, '--label', 'Src'])
        self.assertEqual(p['osc_standard'], 'vrcft-v5-unified')
        self.assertEqual(p['modules'][0]['folder'], 'Assets/VRCFT Jerry')
        self.assertEqual(p['modules'][0]['animated_blendshapes'], ['JawOpen', 'TongueOut'])
        self.assertEqual(p['fbx'][0]['names'], ['JawOpen', 'MouthClosed', 'TongueOut'])
        self.assertEqual(json.loads(open(log).read())['label'], 'Src')

    def test_transfer_dry_run_then_apply(self):
        args = ['transfer', '--source', self.src, '--target', self.dst, '--module', 'Assets/VRCFT Jerry',
                '--target-fbx', 'Assets/Avatar/face.fbx', '--backup-dir', os.path.join(self.tmp.name, 'bk')]
        r = ft.main(args)
        self.assertFalse(os.path.exists(os.path.join(self.dst, 'Assets/VRCFT Jerry')))
        self.assertEqual(r['missing_blendshapes_on_target'], ['TongueOut'])
        self.assertEqual(len(r['files']), 3)
        self.assertEqual({f['file'] for f in r['files']},
                         {'Assets/VRCFT Jerry/FX.controller', 'Assets/VRCFT Jerry/Anim/jaw.anim',
                          'Assets/Shared/blend.asset'})
        self.assertEqual(r['unresolved_guids'], 1)
        ft.main(args + ['--apply'])
        meta = pathlib.Path(self.dst, 'Assets/VRCFT Jerry/Anim/jaw.anim.meta').read_text()
        self.assertIn('c' * 32, meta)

    def test_guid_conflict_blocks_apply(self):
        write(self.dst, 'Assets/Elsewhere/blend.asset', 'x: 2\n', 'd' * 32)
        args = ['transfer', '--source', self.src, '--target', self.dst, '--module', 'Assets/VRCFT Jerry',
                '--backup-dir', os.path.join(self.tmp.name, 'bk')]
        self.assertEqual(len(ft.main(args)['guid_conflicts']), 1)
        with self.assertRaises(SystemExit):
            ft.main(args + ['--apply'])


if __name__ == '__main__':
    unittest.main()
