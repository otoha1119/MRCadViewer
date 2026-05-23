# MRCadViewer

Meta Quest 3 上で FBX 形式の CAD モデルを表示・操作するための Unity プロジェクト．  
Autodesk Fusion 360 で出力した機構モデルの VR / MR 空間内での検証を目的とする．

> [!NOTE]
> 本リポジトリは作者個人による検証用プロジェクトである．Meta，Unity Technologies，Autodesk のいずれにも公式・非公式の関与はない．

---

## 動作環境

| 項目 | バージョン |
| --- | --- |
| 開発 PC | macOS |
| 実機 | Meta Quest 3 |
| Unity | 6000.3.15f1（Universal 3D テンプレート） |
| Meta XR All-in-One SDK | 201.0.0（`com.meta.xr.sdk.all`） |
| OpenXR Plugin | 1.16.1（`com.unity.xr.openxr`） |
| ビルドターゲット | Android（Build Profile: Meta Quest） |
| XR Plug-in Management | Android タブで OpenXR + Meta XR feature group を有効化 |

上記以外の構成は未検証．

---

## 対応範囲

### 実装済み

- FBX モデルのワールド固定表示（Quest 3 単機）
- 左右コントローラのアナログスティックによる回転・拡大縮小（実装中）

### 非対応

- MR パススルー上での重畳表示（VR モードのみ）
- ハンドトラッキング入力
- Quest 2 / Quest Pro / Quest 3S での動作
- 大規模アセンブリ向けの描画最適化（LOD・ドローコール削減等）

---

## 入力仕様

実装は `MRCadViewer/Assets/Scripts/CADJoystickController.cs`．対象 CAD モデルの GameObject にアタッチして使用する．入力取得は `UnityEngine.XR.InputDevices` + `CommonUsages.primary2DAxis` 経由．

| 入力 | 機能 |
| --- | --- |
| 左スティック X 軸 | Y 軸回転（yaw） |
| 左スティック Y 軸 | X 軸回転（pitch） |
| 右スティック X 軸 | Z 軸回転（roll） |
| 右スティック Y 軸 | スケール（`minScaleFactor`〜`maxScaleFactor` でクランプ） |

回転方向の反転は同ファイル内で該当成分の符号を反転して対応．

---

## ビルド手順

1. リポジトリを clone．
2. Fusion 360 から FBX をエクスポートし，`MRCadViewer/Assets/Models/` 配下に配置．
3. Unity Hub から 6000.3.15f1 を導入し，プロジェクトルートを開く．
4. `Meta XR Tools > Project Setup Tool` で **Required** のみ Fix を適用．Recommended は適用しない．
5. Build Profile を **Meta Quest** に切替え，Quest 3 を USB 接続して Build And Run を実行．

### CAD モデルの取り扱い

`*.fbx` は `MRCadViewer/.gitignore` で除外しており，リポジトリには同梱されない．clone 直後の `MRCadViewer/Assets/Models/` には `.fbx.meta` のみが存在する．

Scene が参照していたオリジナルと同一のファイル名・同一のパスで配置した場合，Scene 上のオブジェクト参照は `.fbx.meta` の GUID 経由で復元される．ファイル名またはパスが異なる場合，参照は復元されない．

---


## 再配布・商標利用について

本リポジトリには明示的なライセンスを設定していない．個人利用・改変は自由だが，再配布・フォーク公開を行う場合は事前に作者まで連絡すること．

"Meta"，"Meta Quest"，"Oculus"，"Horizon OS" は Meta Platforms, Inc. の，"Unity" は Unity Technologies の，"Autodesk Fusion 360" は Autodesk, Inc. の，それぞれ商標または登録商標である．本リポジトリはこれらいずれの企業とも提携・後援関係にない非公式の個人プロジェクトであり，各社名・製品名の表記は識別目的での参照に限られる．

## 免責事項

本ソフトウェアは現状有姿 (as-is) で提供されるものであり，動作・安全性・正確性について一切の保証を行わない．本ソフトウェアの利用に起因して発生したいかなる損害 (CAD データの破損・消失，Meta Quest デバイスの不具合，設計情報の漏洩，セキュリティインシデント等を含むがこれに限らない) についても，作者は一切の責任を負わない．利用者自身の責任において使用すること．
