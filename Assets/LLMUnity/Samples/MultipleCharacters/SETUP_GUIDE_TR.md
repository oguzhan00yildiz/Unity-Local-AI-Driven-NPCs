# NPC İnteraction Sistemi - Setup Rehberi

## 1. NPCChatUI Kurulumu (Canvas'a ekle)

### Adım 1: Canvas ve Panel Oluştur
- Hierarchy'de sağ tıkla → UI → Panel → Panel (Canvas ile birlikte)
- Panel'i "ChatPanel" olarak adlandır
- ChatPanel'in özelliklerini ayarla:
  - Anchor Preset: Bottom Right
  - Width: 600, Height: 400
  - Rect Transform: Sağ alt köşeye yerleştir

### Adım 2: Chat UI Öğelerini Oluştur

**a) NPC İsim Text (ChatPanel içinde)**
- ChatPanel'e sağ tıkla → UI → TextMesh Pro → Text
- "NPCNameText" olarak adlandır
- Layout Elements:
  - Height: 50
  - Preferred Height: 50

**b) Chat Display Text (ChatPanel içinde)**
- ChatPanel'e sağ tıkla → UI → Scroll View - TextMesh Pro
- Scroll View'i "ChatScrollView" olarak adlandır
- Viewport içindeki TextMesh Pro Text öğesini "ChatDisplayText" olarak adlandır
- ScrollRect Settings:
  - Vertical ScrollBar: etkinleştir
  - Viewport Height: 280

**c) Input Field (ChatPanel içinde)**
- ChatPanel'e sağ tıkla → UI → TextMesh Pro → Input Field
- "PlayerInputField" olarak adlandır
- Layout Settings:
  - Height: 40
  - Preferred Height: 40

**d) Send Button (ChatPanel içinde)**
- ChatPanel'e sağ tıkla → UI → Button
- "SendButton" olarak adlandır
- Text'i "Gönder" yap (TextMesh Pro Text olacak)
- Layout Settings:
  - Height: 40
  - Preferred Height: 40

**e) Close Button (ChatPanel içinde)**
- ChatPanel'e sağ tıkla → UI → Button
- "CloseButton" olarak adlandır
- Text'i "Kapat" yap (TextMesh Pro Text olacak)
- Layout Settings:
  - Height: 40
  - Preferred Height: 40

### Adım 3: Layout Group Ekle
- ChatPanel'e VerticalLayoutGroup bileşeni ekle
- Settings:
  - Child Force Expand: Height ve Width = false
  - Child Control Size: Height = true, Width = true
  - Padding: 10

## 2. NPC GameObjects'e NPCInteractionController Ekle

### Her NPC için:
1. NPC GameObject'i seç
2. Inspector'da "Add Component" tıkla
3. NPCInteractionController ekle
4. Özelliklerini ayarla:
   - **Interaction Range**: 3 (NPCye yaklaşabileceği mesafe)
   - **NPC Name**: "Barista", "Barman" vb.
   - **LLM Agent**: İlgili LLMAgent GameObject'ini ata

### Trigger Collider Ekle:
1. NPC'ye sağ tıkla → Add Component → Sphere Collider
2. "Is Trigger" = true
3. Radius: 3 (Interaction Range ile aynı)

## 3. Player GameObject'ini Ayarla

1. Player GameObject'i seç
2. Tag'i "Player" olarak ayarla (Tag Manager'da ekle)

## 4. NPCChatUI Script'ini Yöneticide Ayarla

1. Boş bir GameObject oluştur: "ChatManager"
2. NPCChatUI script'ini buna ekle
3. Inspector'da referansları ata:
   - **Chat Panel**: ChatPanel (yukarıda oluşturduğun)
   - **NPC Name Text**: NPCNameText
   - **Chat Display Text**: ChatDisplayText
   - **Player Input Field**: PlayerInputField
   - **Send Button**: SendButton
   - **Close Button**: CloseButton
   - **Chat Scroll Rect**: ChatScrollView'in ScrollRect bileşeni

## 5. Scene Kurulumunun Nihai Hali

```
Scene
├── Player (Tag: "Player")
│   └── Camera (Main Camera)
├── NPC_1 (örn: Barista)
│   ├── Model (3D Model)
│   ├── Sphere Collider (Trigger)
│   ├── LLMAgent
│   └── NPCInteractionController
├── NPC_2 (örn: Barman)
│   ├── Model (3D Model)
│   ├── Sphere Collider (Trigger)
│   ├── LLMAgent
│   └── NPCInteractionController
└── Canvas
    ├── ChatManager (NPCChatUI script)
    └── ChatPanel (aktif = false)
        ├── NPCNameText
        ├── ChatScrollView
        │   └── Viewport
        │       └── ChatDisplayText
        ├── PlayerInputField
        ├── SendButton
        └── CloseButton
```

## Kullanım

1. NPCye doğru yürü
2. NPC'ye yaklaş (interaction range içinde)
3. E tuşuna bas → Chat penceresi açılır
4. Mesaj yaz ve Enter/Gönder tuşuna bas
5. NPC yanıt verecek
6. ESC veya Kapat butonuyla pencereyi kapat

## Önemli Notlar

- Her NPC'nin ayrı bir **LLMAgent** component'i olması gerekir
- Player'ın **Camera.main** tag'ine sahip bir kamerası olmalı
- Chat paneli varsayılan olarak kapalı (inactive) durumda başlar
- Mesajlar otomatik scroll edilir
- Son 10 mesaj gösterilir (maxDisplayMessages'te ayarlanabilir)

## Opsiyonel Ayarlamalar

### Oyunu Duraklat
NPCChatUI.cs'de bu satırları aktifleştir:
- OpenChat() metodunda: `Time.timeScale = 0f;`
- CloseChat() metodunda: `Time.timeScale = 1f;`

### Başka Tuş Kullan
NPCInteractionController'de `interactionKey` değişkenini değiştir

### Chat Panelinin Görünümünü Özelleştir
- Renk, font, boyut vb. Canvas'ta doğrudan UI öğelerine editleri yap
