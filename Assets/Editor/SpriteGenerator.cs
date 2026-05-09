
using System.IO;
using UnityEditor;
using UnityEngine;

public static class SpriteGenerator
{
    [MenuItem("Tools/Generate Game Sprites")]
    public static void GenerateAll()
    {
        Directory.CreateDirectory("Assets/Sprites/BG");
        Directory.CreateDirectory("Assets/Sprites/Char");
        Directory.CreateDirectory("Assets/Sprites/Env");

        MakeBgManagement();
        MakeBgDungeon();
        MakeBgRestaurant();
        MakePlayer();
        MakeCustomer();
        MakeSlime();
        MakeFarmPlot();
        MakePortal();
        MakeToolStation();
        MakeShopStation();
        MakeTable();
        MakeCookStation();
        MakeDungeonExit();
        MakeEveningSign();
        MakeItemDrop();

        AssetDatabase.Refresh();
        Debug.Log("[SpriteGenerator] Done - 15 sprites created.");
    }

    // helpers
    static void Save(Texture2D t, string path, int ppu = 32)
    {
        File.WriteAllBytes(path, t.EncodeToPNG());
        AssetDatabase.ImportAsset(path);
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.textureType         = TextureImporterType.Sprite;
        imp.spritePixelsPerUnit = ppu;
        imp.filterMode          = FilterMode.Point;
        imp.textureCompression  = TextureImporterCompression.Uncompressed;
        imp.alphaIsTransparency = true;
        imp.SaveAndReimport();
    }

    static bool Ellipse(int x, int y, float cx, float cy, float rx, float ry)
    {
        float ex = (x - cx) / rx, ey = (y - cy) / ry;
        return ex * ex + ey * ey < 1f;
    }
    static bool Circle(int x, int y, float cx, float cy, float r)
        => (x - cx) * (x - cx) + (y - cy) * (y - cy) < r * r;

    static Texture2D Tex(int w, int h) => new Texture2D(w, h, TextureFormat.RGBA32, false);
    static Color C(float r, float g, float b, float a = 1f) => new Color(r, g, b, a);

    // Backgrounds 640x360 @ 32 PPU
    static void MakeBgManagement()
    {
        var t = Tex(640, 360); var px = new Color[640 * 360];
        var sky1 = C(0.53f,0.81f,0.98f); var sky2 = C(0.75f,0.93f,1f);
        var gnd1 = C(0.22f,0.55f,0.13f); var gnd2 = C(0.35f,0.70f,0.22f);
        for (int y = 0; y < 360; y++) for (int x = 0; x < 640; x++)
        {
            var c = y < 110
                ? Color.Lerp(gnd1, gnd2, y / 109f)
                : Color.Lerp(sky1, sky2, (y - 110) / 250f);
            if (y < 110 && (y / 12) % 2 == 0) c *= 0.92f;
            px[y * 640 + x] = c;
        }
        t.SetPixels(px); t.Apply();
        Save(t, "Assets/Sprites/BG/BG_Management.png", 32);
    }

    static void MakeBgDungeon()
    {
        var t = Tex(640, 360); var px = new Color[640 * 360];
        var floor  = C(0.18f,0.14f,0.12f);
        var wall   = C(0.12f,0.10f,0.14f);
        var mortar = C(0.08f,0.06f,0.09f);
        for (int y = 0; y < 360; y++) for (int x = 0; x < 640; x++)
        {
            bool isFloor  = y < 90;
            bool isMortar = (x % 40 == 0) || (y % 20 == 0);
            var c = isFloor ? floor : wall;
            if (isMortar) c = mortar;
            else if (((x / 40) + (y / 20)) % 3 == 0) c *= 1.08f;
            px[y * 640 + x] = c;
        }
        t.SetPixels(px); t.Apply();
        Save(t, "Assets/Sprites/BG/BG_Dungeon.png", 32);
    }

    static void MakeBgRestaurant()
    {
        var t = Tex(640, 360); var px = new Color[640 * 360];
        var flr1 = C(0.50f,0.32f,0.14f); var flr2 = C(0.65f,0.45f,0.22f);
        var wll1 = C(0.82f,0.72f,0.60f); var wll2 = C(0.78f,0.67f,0.54f);
        for (int y = 0; y < 360; y++) for (int x = 0; x < 640; x++)
        {
            bool isFloor = y < 120;
            var c = isFloor
                ? ((x / 16) % 2 == 0 ? flr1 : flr2)
                : ((y / 48) % 2 == 0 ? wll1 : wll2);
            if (isFloor && (y < 4 || y > 116)) c *= 0.70f;
            px[y * 640 + x] = c;
        }
        t.SetPixels(px); t.Apply();
        Save(t, "Assets/Sprites/BG/BG_Restaurant.png", 32);
    }

    // Characters
    static void MakePlayer()
    {
        var t = Tex(32, 48); var px = new Color[32 * 48];
        var skin = C(0.97f,0.82f,0.65f); var body = C(0.18f,0.42f,0.90f);
        var boot = C(0.10f,0.20f,0.55f); var hair = C(0.30f,0.18f,0.07f);
        var eye  = C(0.10f,0.10f,0.10f);
        for (int y = 0; y < 48; y++) for (int x = 0; x < 32; x++)
        {
            var c = Color.clear;
            if (y < 16 && x >= 7  && x <= 11) c = y < 4 ? boot : body;
            if (y < 16 && x >= 20 && x <= 24) c = y < 4 ? boot : body;
            if (y >= 16 && y < 32 && x >= 6 && x <= 25) c = body;
            if (y >= 20 && y < 28 && x >= 3 && x <= 5)  c = body;
            if (y >= 20 && y < 28 && x >= 26 && x <= 28) c = body;
            if (Circle(x, y, 15.5f, 40f, 8f))  c = skin;
            if (Circle(x, y, 15.5f, 40f, 8f) && y >= 40) c = hair;
            if ((x == 11 || x == 12) && (y == 38 || y == 39)) c = eye;
            if ((x == 19 || x == 20) && (y == 38 || y == 39)) c = eye;
            px[y * 32 + x] = c;
        }
        t.SetPixels(px); t.Apply();
        Save(t, "Assets/Sprites/Char/Player.png");
    }

    static void MakeCustomer()
    {
        var t = Tex(28, 44); var px = new Color[28 * 44];
        var skin  = C(0.97f,0.82f,0.65f); var shirt = C(0.95f,0.55f,0.10f);
        var pants = C(0.30f,0.20f,0.10f); var boot  = C(0.20f,0.12f,0.05f);
        var eye   = C(0.10f,0.10f,0.10f); var mouth = C(0.55f,0.22f,0.10f);
        for (int y = 0; y < 44; y++) for (int x = 0; x < 28; x++)
        {
            var c = Color.clear;
            if (y < 12 && x >= 5  && x <= 9)  c = y < 3 ? boot : pants;
            if (y < 12 && x >= 18 && x <= 22) c = y < 3 ? boot : pants;
            if (y >= 12 && y < 28 && x >= 5 && x <= 22) c = shirt;
            if (y >= 20 && y < 27 && x >= 2 && x <= 4)  c = shirt;
            if (y >= 20 && y < 27 && x >= 23 && x <= 25) c = shirt;
            if (Circle(x, y, 13.5f, 36f, 7f)) c = skin;
            if ((x == 9  || x == 10) && (y == 34 || y == 35)) c = eye;
            if ((x == 17 || x == 18) && (y == 34 || y == 35)) c = eye;
            if (y == 32 && x >= 11 && x <= 16) c = mouth;
            px[y * 28 + x] = c;
        }
        t.SetPixels(px); t.Apply();
        Save(t, "Assets/Sprites/Char/Customer.png");
    }

    static void MakeSlime()
    {
        var t = Tex(40, 36); var px = new Color[40 * 36];
        var body  = C(0.30f,0.85f,0.28f); var dark2 = C(0.18f,0.55f,0.15f);
        var shine = C(0.70f,1.00f,0.60f);
        for (int y = 0; y < 36; y++) for (int x = 0; x < 40; x++)
        {
            var c = Color.clear;
            if (Ellipse(x, y, 20f,15f,18f,14f)) c = body;
            if (Ellipse(x, y, 20f,17f,14f, 9f)) c = Color.Lerp(body, dark2, 0.4f);
            if (Circle(x, y, 13f,22f, 5f)) c = dark2;
            if (Circle(x, y, 13f,22f, 3f)) c = shine;
            if (Circle(x, y, 14f,17f, 2.5f)) c = Color.white;
            if (Circle(x, y, 26f,17f, 2.5f)) c = Color.white;
            if (Circle(x, y, 14.5f,17.5f, 1.2f)) c = C(0.1f,0.1f,0.1f);
            if (Circle(x, y, 26.5f,17.5f, 1.2f)) c = C(0.1f,0.1f,0.1f);
            px[y * 40 + x] = c;
        }
        t.SetPixels(px); t.Apply();
        Save(t, "Assets/Sprites/Char/Slime.png");
    }

    // Environment
    static void MakeFarmPlot()
    {
        var t = Tex(64, 40); var px = new Color[64 * 40];
        var soil = C(0.42f,0.27f,0.12f); var dark2 = C(0.28f,0.16f,0.06f);
        var row  = C(0.36f,0.22f,0.09f);
        for (int y = 0; y < 40; y++) for (int x = 0; x < 64; x++)
        {
            bool border = x < 2 || x > 61 || y < 2 || y > 37;
            px[y * 64 + x] = border ? dark2 : (x / 8) % 2 == 0 ? soil : row;
        }
        t.SetPixels(px); t.Apply();
        Save(t, "Assets/Sprites/Env/FarmPlot.png");
    }

    static void MakePortal()
    {
        var t = Tex(48, 72); var px = new Color[48 * 72];
        var frame = C(0.40f,0.25f,0.60f); var glow  = C(0.60f,0.20f,0.90f);
        var inner = C(0.85f,0.55f,1.00f); var outer = C(0.25f,0.08f,0.45f);
        for (int y = 0; y < 72; y++) for (int x = 0; x < 48; x++)
        {
            var c = Color.clear;
            float ex = (x-24f)/20f, ey = (y-38f)/30f, d = ex*ex+ey*ey;
            if (d < 1.00f) c = outer;
            if (d < 0.75f) c = glow;
            if (d < 0.42f) c = inner;
            if ((x < 6 || x > 41) && y < 54) c = frame;
            if (Ellipse(x,y,24f,42f,22f,32f) && !Ellipse(x,y,24f,42f,18f,27f)) c = frame;
            px[y * 48 + x] = c;
        }
        t.SetPixels(px); t.Apply();
        Save(t, "Assets/Sprites/Env/Portal.png");
    }

    static void MakeToolStation()
    {
        var t = Tex(48, 48); var px = new Color[48 * 48];
        var metal = C(0.55f,0.58f,0.65f); var dark2 = C(0.28f,0.30f,0.36f);
        var scrn  = C(0.06f,0.35f,0.18f); var sword = C(0.75f,0.80f,0.90f);
        var hilt  = C(0.60f,0.40f,0.10f); var btn   = C(0.90f,0.20f,0.15f);
        for (int y = 0; y < 48; y++) for (int x = 0; x < 48; x++)
        {
            bool border = x < 3 || x > 44 || y < 3 || y > 44;
            bool screen = x >= 6  && x <= 22 && y >= 20 && y <= 38;
            bool blade  = screen  && x >= 10 && x <= 13 && y >= 22 && y <= 36;
            bool hiltPx = screen  && (y == 26 || y == 27) && x >= 8 && x <= 17;
            bool button = x >= 28 && x <= 42 && y >= 28 && y <= 36;
            var c = border ? dark2 : metal;
            if (screen) c = scrn;
            if (blade)  c = sword;
            if (hiltPx) c = hilt;
            if (button) c = btn;
            px[y * 48 + x] = c;
        }
        t.SetPixels(px); t.Apply();
        Save(t, "Assets/Sprites/Env/ToolStation.png");
    }

    static void MakeShopStation()
    {
        var t = Tex(48, 48); var px = new Color[48 * 48];
        var wood  = C(0.55f,0.38f,0.18f); var woodD = C(0.35f,0.22f,0.09f);
        var gold  = C(1.00f,0.82f,0.10f); var goldD = C(0.80f,0.60f,0.05f);
        for (int y = 0; y < 48; y++) for (int x = 0; x < 48; x++)
        {
            bool border  = x < 3 || x > 44 || y < 3 || y > 44;
            bool counter = y < 18;
            var c = (border || counter) ? woodD : wood;
            if (Circle(x,y,24f,30f,12f)) c = goldD;
            if (Circle(x,y,24f,30f, 9f)) c = gold;
            if (Circle(x,y,24f,30f, 4f)) c = goldD;
            if (y >= 28 && y <= 32 && x >= 21 && x <= 27) c = woodD;
            if (y >= 25 && y <= 35 && x == 21)             c = woodD;
            px[y * 48 + x] = c;
        }
        t.SetPixels(px); t.Apply();
        Save(t, "Assets/Sprites/Env/ShopStation.png");
    }

    static void MakeTable()
    {
        var t = Tex(64, 48); var px = new Color[64 * 48];
        var top  = C(0.62f,0.42f,0.22f); var topD = C(0.50f,0.32f,0.14f);
        var leg  = C(0.40f,0.25f,0.10f);
        for (int y = 0; y < 48; y++) for (int x = 0; x < 64; x++)
        {
            var c = Color.clear;
            if (y >= 28) c = (x / 12) % 2 == 0 ? top : topD;
            if (y >= 28 && (y > 45 || y < 30)) c = leg;
            if (x >= 6  && x <= 12 && y < 28) c = leg;
            if (x >= 51 && x <= 57 && y < 28) c = leg;
            px[y * 64 + x] = c;
        }
        t.SetPixels(px); t.Apply();
        Save(t, "Assets/Sprites/Env/Table.png");
    }

    static void MakeCookStation()
    {
        var t = Tex(56, 56); var px = new Color[56 * 56];
        var stove = C(0.22f,0.22f,0.28f); var stovD = C(0.14f,0.14f,0.18f);
        var pot   = C(0.30f,0.30f,0.35f); var potD  = C(0.18f,0.18f,0.22f);
        var fire1 = C(1.00f,0.35f,0.00f); var fire2 = C(1.00f,0.80f,0.00f);
        for (int y = 0; y < 56; y++) for (int x = 0; x < 56; x++)
        {
            var c = Color.clear;
            bool baseA = x >= 4 && x <= 51 && y >= 4 && y <= 32;
            if (baseA) c = (x < 7 || x > 48 || y < 7 || y > 29) ? stovD : stove;
            if (Ellipse(x,y,28f,38f,20f,16f)) c = pot;
            if (Ellipse(x,y,28f,38f,17f,13f)) c = potD;
            if (y >= 50 && y <= 53 && x >= 10 && x <= 46) c = pot;
            if (y >= 18 && y <= 34 && Ellipse(x,y,28f,27f,8f,8f)) c = fire1;
            if (y >= 21 && y <= 32 && Ellipse(x,y,28f,27f,5f,5f)) c = fire2;
            px[y * 56 + x] = c;
        }
        t.SetPixels(px); t.Apply();
        Save(t, "Assets/Sprites/Env/CookStation.png");
    }

    static void MakeDungeonExit()
    {
        var t = Tex(40, 56); var px = new Color[40 * 56];
        var stone  = C(0.35f,0.32f,0.28f); var stoneD = C(0.22f,0.20f,0.18f);
        var door   = C(0.48f,0.30f,0.14f); var doorD  = C(0.30f,0.18f,0.08f);
        var handle = C(0.50f,0.50f,0.60f);
        for (int y = 0; y < 56; y++) for (int x = 0; x < 40; x++)
        {
            var c = ((x / 8) + (y / 8)) % 2 == 0 ? stoneD : stone;
            bool inDoor = x >= 6 && x <= 33 && y < 42;
            bool inArch = Ellipse(x,y,20f,42f,14f,12f);
            if (inDoor || inArch)
                c = ((x - 6) % 8 < 2 || y % 8 < 2) ? doorD : door;
            if (Circle(x,y,25f,20f,3f)) c = handle;
            px[y * 40 + x] = c;
        }
        t.SetPixels(px); t.Apply();
        Save(t, "Assets/Sprites/Env/DungeonExit.png");
    }

    static void MakeEveningSign()
    {
        var t = Tex(48, 56); var px = new Color[48 * 56];
        var sign  = C(0.65f,0.42f,0.18f); var signD = C(0.42f,0.26f,0.09f);
        var post  = C(0.38f,0.24f,0.10f); var txt   = C(1f,0.85f,0.1f);
        for (int y = 0; y < 56; y++) for (int x = 0; x < 48; x++)
        {
            var c = Color.clear;
            if (x >= 21 && x <= 26) c = post;
            if (x >= 3 && x <= 44 && y >= 20 && y <= 50)
                c = (x < 6 || x > 41 || y < 23 || y > 47) ? signD : sign;
            if (y >= 28 && y <= 44 && x >= 8 && x <= 40)
            {
                bool bar = y == 28 || y == 36 || y == 44
                        || x == 8 || x == 16 || x == 24 || x == 32 || x == 40;
                if (bar) c = txt;
            }
            px[y * 48 + x] = c;
        }
        t.SetPixels(px); t.Apply();
        Save(t, "Assets/Sprites/Env/EveningSign.png");
    }

    static void MakeItemDrop()
    {
        var t = Tex(24, 24); var px = new Color[24 * 24];
        var gem  = C(0.20f,0.90f,0.40f);
        var gemL = C(0.70f,1.00f,0.80f);
        var gemD = C(0.10f,0.55f,0.22f);
        for (int y = 0; y < 24; y++) for (int x = 0; x < 24; x++)
        {
            float d = Mathf.Abs(x - 12) + Mathf.Abs(y - 12);
            var c = Color.clear;
            if (d < 10f) c = gem;
            if (d <  4f) c = gemL;
            if (d >= 7f && d < 10f) c = gemD;
            px[y * 24 + x] = c;
        }
        t.SetPixels(px); t.Apply();
        Save(t, "Assets/Sprites/Env/ItemDrop.png");
    }
}
