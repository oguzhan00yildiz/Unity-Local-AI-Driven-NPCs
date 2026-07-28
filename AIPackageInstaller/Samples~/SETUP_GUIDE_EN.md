# NPC Interaction System - Setup Guide

## 1. UI Setup (Add to Canvas)

### Step 1: Create Canvas & Chat Panel
- Right-click in **Hierarchy** → **UI** → **Panel** (creates Canvas if needed).
- Rename the Panel to `ChatPanel`.
- Configure `ChatPanel` RectTransform:
  - **Anchor Preset**: Bottom Right
  - **Width**: `600`, **Height**: `400`
  - Position it in the bottom-right corner.

### Step 2: Create Chat UI Elements

#### a) NPC Name Text (Inside `ChatPanel`)
- Right-click `ChatPanel` → **UI** → **TextMeshPro - Text**.
- Rename to `NPCNameText`.
- **Layout Element**: Set Height / Preferred Height to `50`.

#### b) Chat Display Text (Inside `ChatPanel`)
- Right-click `ChatPanel` → **UI** → **Scroll View - TextMeshPro**.
- Rename Scroll View to `ChatScrollView`.
- Rename the TextMeshPro Text inside `Viewport` to `ChatDisplayText`.
- **ScrollRect Settings**: Enable Vertical Scrollbar; set Viewport Height to `280`.

#### c) Input Field (Inside `ChatPanel`)
- Right-click `ChatPanel` → **UI** → **TextMeshPro - Input Field**.
- Rename to `PlayerInputField`.
- **Layout Element**: Set Height / Preferred Height to `40`.

#### d) Send Button (Inside `ChatPanel`)
- Right-click `ChatPanel` → **UI** → **Button - TextMeshPro**.
- Rename to `SendButton`.
- Set Text content to `Send`.
- **Layout Element**: Set Height / Preferred Height to `40`.

#### e) Close Button (Inside `ChatPanel`)
- Right-click `ChatPanel` → **UI** → **Button - TextMeshPro**.
- Rename to `CloseButton`.
- Set Text content to `Close`.
- **Layout Element**: Set Height / Preferred Height to `40`.

### Step 3: Add Layout Group
- Add a `Vertical Layout Group` component to `ChatPanel`.
- **Settings**:
  - **Child Force Expand**: Width = `false`, Height = `false`
  - **Child Control Size**: Width = `true`, Height = `true`
  - **Padding**: `10`

---

## 2. Configure NPC GameObjects (`NPCInteractionController`)

### For Each NPC:
1. Select the NPC GameObject in the Hierarchy.
2. Click **Add Component** in the Inspector.
3. Search and add `NPCInteractionController`.
4. Configure fields:
   - **Interaction Range**: `3` (Distance required to talk to NPC)
   - **NPC Name**: "Barista", "Barman", etc.
   - **LLM Agent**: Assign the corresponding `LLMAgent` GameObject.

### Add Trigger Collider:
1. On the NPC GameObject, click **Add Component** → **Sphere Collider**.
2. Check **Is Trigger** = `true`.
3. Set **Radius** to `3` (matches Interaction Range).

---

## 3. Configure Player GameObject

1. Select your **Player** GameObject.
2. Ensure its Tag is set to **`Player`** (Add via Tag Manager if missing).

---

## 4. Configure `NPCChatUI` Script Manager

1. Create an empty GameObject named `ChatManager`.
2. Attach the `NPCChatUI` script component to `ChatManager`.
3. Assign references in the Inspector:
   - **Chat Panel**: `ChatPanel`
   - **NPC Name Text**: `NPCNameText`
   - **Chat Display Text**: `ChatDisplayText`
   - **Player Input Field**: `PlayerInputField`
   - **Send Button**: `SendButton`
   - **Close Button**: `CloseButton`
   - **Chat Scroll Rect**: `ScrollRect` component of `ChatScrollView`

---

## 5. Final Scene Hierarchy Structure

```text
Scene
├── Player (Tag: "Player")
│   └── Main Camera
├── NPC_1 (e.g. Barista)
│   ├── Model (3D Model)
│   ├── Sphere Collider (Is Trigger = true)
│   ├── LLMAgent
│   └── NPCInteractionController
├── NPC_2 (e.g. Barman)
│   ├── Model (3D Model)
│   ├── Sphere Collider (Is Trigger = true)
│   ├── LLMAgent
│   └── NPCInteractionController
└── Canvas
    ├── ChatManager (NPCChatUI script)
    └── ChatPanel (Disabled/Inactive by default)
        ├── NPCNameText
        ├── ChatScrollView
        │   └── Viewport
        │       └── ChatDisplayText
        ├── PlayerInputField
        ├── SendButton
        └── CloseButton
```

---

## How to Use

1. Walk your player toward an NPC.
2. Enter the interaction range radius.
3. Press **`E`** to open the Chat UI window.
4. Type your message into the input field and press **Enter** or click **Send**.
5. The NPC will respond locally using LLM.
6. Press **`ESC`** or click **Close** to exit dialogue mode.

---

## Important Notes

- Each NPC requires its own dedicated **`LLMAgent`** component.
- The Player must have a camera tagged as **`MainCamera`**.
- The `ChatPanel` starts in an **inactive/disabled** state by default.
- Messages auto-scroll to the bottom.
- Displays up to 10 recent messages (configurable via `maxDisplayMessages`).

---

## Optional Customizations

- **Pause Game During Chat**: Enable `Time.timeScale = 0f;` in `OpenChat()` and `Time.timeScale = 1f;` in `CloseChat()` inside `NPCChatUI.cs`.
- **Change Interaction Key**: Modify `interactionKey` in `NPCInteractionController.cs`.
- **Custom UI Style**: Customize colors, fonts, and sizes directly on the Canvas UI elements.
