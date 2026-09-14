using System.Collections.Generic;
using UnityEngine;

namespace SparkBossFramework001.Unity
{
    // All meshes and materials are transient children of the sandbox only.
    public sealed class BossSandboxVisuals
    {
        public readonly Transform Root;
        public readonly Transform Add;
        public readonly Transform[] Runes = new Transform[3];
        public readonly Transform Hazard;
        public readonly Transform Swing;
        public readonly Transform Aim;
        private readonly List<Material> _materials = new List<Material>();
        private readonly Transform _shield;
        private readonly LineRenderer _tether;
        private readonly Transform _boss;
        private readonly Material _glow, _gold, _danger, _stone, _dark, _cyan, _white;
        private readonly MaterialPropertyBlock _block = new MaterialPropertyBlock();
        private readonly Transform _bossCrown;

        public BossSandboxVisuals(Transform parent, Transform boss, Transform player)
        {
            Root=new GameObject("SandboxVisualsRuntime").transform;Root.SetParent(parent,false);_boss=boss;
            _stone=Mat(new Color(.39f,.46f,.52f),false);_dark=Mat(new Color(.10f,.17f,.23f),false);
            _glow=Mat(new Color(.10f,.85f,1f),true);_gold=Mat(new Color(1f,.68f,.18f),true);
            _danger=Mat(new Color(1f,.24f,.12f),true);_cyan=Mat(new Color(.14f,.9f,.7f),false);_white=Mat(new Color(1f,.96f,.8f),true);
            // Raised tiles, arena perimeter and contrasting cardinal markers.
            for(int x=-3;x<=3;x++)for(int z=-3;z<=3;z++)
                Piece("FloorTile",PrimitiveType.Cube,Root,new Vector3(x*1.85f,-.08f,z*1.85f),new Vector3(1.78f,.12f,1.78f),(x+z)%2==0?_stone:_dark);
            Ring("ArenaBorder",Root,Vector3.zero,6.55f,_gold,.06f);
            for(int i=0;i<12;i++)
            {
                float a=i*Mathf.PI/6;var pos=new Vector3(Mathf.Sin(a)*6.65f,.35f,Mathf.Cos(a)*6.65f);
                Piece("BoundaryPillar",PrimitiveType.Cube,Root,pos,new Vector3(.4f,.8f,.4f),_stone);
                Piece("BoundaryLight",PrimitiveType.Cube,Root,pos+Vector3.up*.47f,new Vector3(.3f,.09f,.3f),_gold);
            }
            // Golem silhouette on the existing boss capsule.
            Piece("GuardianMask",PrimitiveType.Cube,boss,new Vector3(0,.5f,-.43f),new Vector3(.85f,.65f,.26f),_dark);
            Piece("GuardianEyeL",PrimitiveType.Cube,boss,new Vector3(-.2f,.54f,-.58f),new Vector3(.20f,.09f,.04f),_gold);
            Piece("GuardianEyeR",PrimitiveType.Cube,boss,new Vector3(.2f,.54f,-.58f),new Vector3(.20f,.09f,.04f),_gold);
            Piece("ShoulderL",PrimitiveType.Cube,boss,new Vector3(-.65f,.08f,0),new Vector3(.5f,.62f,.6f),_stone);
            Piece("ShoulderR",PrimitiveType.Cube,boss,new Vector3(.65f,.08f,0),new Vector3(.5f,.62f,.6f),_stone);
            Piece("ChestCore",PrimitiveType.Sphere,boss,new Vector3(0,-.12f,-.51f),new Vector3(.3f,.3f,.13f),_gold);
            _bossCrown=Ring("GuardianCrown",boss,new Vector3(0,1.1f,0),.58f,_gold,.045f).transform;
            Piece("PlayerVisor",PrimitiveType.Cube,player,new Vector3(0,.5f,.43f),new Vector3(.65f,.17f,.12f),_white);
            Piece("PlayerBlaster",PrimitiveType.Cube,player,new Vector3(.48f,-.1f,.35f),new Vector3(.2f,.25f,.65f),_dark);
            Piece("BlasterMuzzle",PrimitiveType.Cube,player,new Vector3(.48f,-.1f,.72f),new Vector3(.17f,.15f,.10f),_glow);
            Ring("PlayerFootRing",player,new Vector3(0,-.95f,0),.65f,_glow,.035f);
            Paint(player.GetComponent<Renderer>(),new Color(.18f,.85f,.65f));
            Add=Piece("ShieldKeeper",PrimitiveType.Capsule,Root,new Vector3(4.5f,1,0),new Vector3(.75f,.85f,.75f),_dark);
            Piece("KeeperCore",PrimitiveType.Sphere,Add,new Vector3(0,.3f,-.48f),new Vector3(.5f,.5f,.24f),_glow);
            Piece("KeeperWingL",PrimitiveType.Cube,Add,new Vector3(-.65f,.25f,0),new Vector3(.65f,.2f,.45f),_gold);
            Piece("KeeperWingR",PrimitiveType.Cube,Add,new Vector3(.65f,.25f,0),new Vector3(.65f,.2f,.45f),_gold);
            Ring("KeeperRing",Add,new Vector3(0,-.8f,0),.9f,_glow,.04f);
            var points=new[]{new Vector3(-4.5f,.15f,3.5f),new Vector3(4.5f,.15f,3.5f),new Vector3(0,.15f,-4.5f)};
            for(int i=0;i<3;i++)
            {
                Runes[i]=Piece("Rune_"+(i+1),PrimitiveType.Cylinder,Root,points[i],new Vector3(1.4f,.15f,1.4f),_dark);
                Ring("RuneInscription",Runes[i],new Vector3(0,1.1f,0),.44f,_gold,.07f);
                var crystal=Piece("RuneCrystal",PrimitiveType.Cube,Runes[i],new Vector3(0,3,0),new Vector3(.26f,2.4f,.26f),_gold);
                crystal.localRotation=Quaternion.Euler(0,45,0);
            }
            _shield=new GameObject("BossRangedShield").transform;_shield.SetParent(Root,false);
            Ring("ShieldEquator",_shield,Vector3.zero,1.85f,_glow,.045f);
            var ring=Ring("ShieldMeridianA",_shield,Vector3.zero,1.85f,_glow,.04f);ring.transform.localRotation=Quaternion.Euler(90,0,0);
            ring=Ring("ShieldMeridianB",_shield,Vector3.zero,1.85f,_glow,.04f);ring.transform.localRotation=Quaternion.Euler(90,90,0);
            _tether=Line("ShieldSourceTether",Root,_glow,.07f,2);
            Hazard=Piece("BossWarning",PrimitiveType.Cylinder,Root,Vector3.zero,Vector3.one,_danger);Hazard.gameObject.SetActive(false);
            Swing=Piece("PlayerSlash",PrimitiveType.Cube,Root,Vector3.zero,Vector3.one,_white);Swing.gameObject.SetActive(false);
            Aim=Ring("AimReticle",Root,Vector3.zero,.5f,_white,.045f).transform;
        }

        private Material Mat(Color color,bool unlit)
        {
            var m=new Material(Shader.Find(unlit?"Universal Render Pipeline/Unlit":"Universal Render Pipeline/Lit"));m.SetColor("_BaseColor",color);
            if(!unlit){m.SetFloat("_Smoothness",.25f);m.SetFloat("_Metallic",.2f);} _materials.Add(m);return m;
        }
        private Transform Piece(string name,PrimitiveType shape,Transform parent,Vector3 pos,Vector3 scale,Material mat)
        {
            var o=GameObject.CreatePrimitive(shape);o.name=name;o.transform.SetParent(parent,false);o.transform.localPosition=pos;o.transform.localScale=scale;
            Object.Destroy(o.GetComponent<Collider>());o.GetComponent<Renderer>().sharedMaterial=mat;return o.transform;
        }
        private LineRenderer Line(string name,Transform parent,Material mat,float width,int count)
        {
            var o=new GameObject(name);o.transform.SetParent(parent,false);var line=o.AddComponent<LineRenderer>();line.useWorldSpace=false;line.sharedMaterial=mat;line.widthMultiplier=width;line.positionCount=count;return line;
        }
        private LineRenderer Ring(string name,Transform parent,Vector3 pos,float radius,Material mat,float width)
        {
            var line=Line(name,parent,mat,width,64);line.loop=true;line.transform.localPosition=pos+Vector3.up*.045f;
            for(int i=0;i<64;i++){float a=i*Mathf.PI*2/64;line.SetPosition(i,new Vector3(Mathf.Cos(a)*radius,0,Mathf.Sin(a)*radius));}return line;
        }
        public Transform Projectile(Vector3 position)
        {
            var orb=Piece("PlayerBolt",PrimitiveType.Sphere,Root,position,new Vector3(.22f,.22f,.22f),_glow);
            var trail=orb.gameObject.AddComponent<TrailRenderer>();trail.sharedMaterial=_glow;trail.time=.12f;trail.startWidth=.16f;trail.endWidth=0;trail.minVertexDistance=.08f;return orb;
        }
        public Transform Burst(Vector3 position,Color color)
        {
            var t=Ring("HitBurst",Root,position,.3f,_white,.06f).transform;
            t.GetComponent<LineRenderer>().startColor=color;t.GetComponent<LineRenderer>().endColor=color;return t;
        }
        public void Paint(Renderer target,Color color)
        {
            if(target==null)return;_block.Clear();_block.SetColor("_BaseColor",color);_block.SetColor("_Color",color);target.SetPropertyBlock(_block);
        }
        public void Refresh(float time,bool rangedShield,bool runeShield,bool addAlive,bool[] runes,Vector3 aim,Color bossColor)
        {
            _shield.position=_boss.position;_shield.rotation=Quaternion.Euler(0,time*30,0);_shield.gameObject.SetActive(rangedShield || runeShield);
            foreach(var line in _shield.GetComponentsInChildren<LineRenderer>())line.sharedMaterial=runeShield?_gold:_glow;
            _tether.gameObject.SetActive(addAlive);if(addAlive){_tether.SetPosition(0,Root.InverseTransformPoint(Add.position));_tether.SetPosition(1,Root.InverseTransformPoint(_boss.position));}
            Add.gameObject.SetActive(addAlive);Aim.position=new Vector3(aim.x,.1f,aim.z);
            _bossCrown.localRotation=Quaternion.Euler(0,time*18,0);Paint(_boss.GetComponent<Renderer>(),bossColor);
            for(int i=0;i<3;i++)Paint(Runes[i].GetComponent<Renderer>(),runes[i]?new Color(.1f,.85f,.55f):runeShield?new Color(.8f,.45f,.12f):new Color(.18f,.24f,.3f));
        }
        public void Dispose(){foreach(var m in _materials)if(m!=null)Object.Destroy(m);if(Root!=null)Object.Destroy(Root);}
    }
}
