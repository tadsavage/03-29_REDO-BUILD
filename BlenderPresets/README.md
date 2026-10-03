# Blender export presets

`Unity_Avatar.py` is the saved FBX export preset for avatar parts (Blender 5.2). Always export with it so the axis
settings are identical in every file.

Install on a PC: copy it to

```
%APPDATA%\Blender Foundation\Blender\5.2\scripts\presets\operator\export_scene.fbx\Unity_Avatar.py
```

Then File > Export > FBX, choose `Unity_Avatar` in the preset dropdown at the top of the export panel.

The file must stay **UTF-8 without BOM**, or Blender rejects it. Details and the export checklist:
`Assets/_Project/_Avatar_System/Docs/BLENDER_CHECKLIST.md` and `AVATAR_SLOTS_AND_BODY_FALLBACK.md`.
