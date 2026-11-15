using System;
using TMPro;
using UnityEngine;
using Wynnyng.Configuration;
using Wynnyng.Core;

namespace Wynnyng.Presentation
{
    /// <summary>Original blockout footballers. Physics owns roots; this class animates visual children only.</summary>
    public sealed class FootballPlayerVisuals
    {
        private readonly Transform[] bodies, leftLegs, rightLegs, leftArms, rightArms;
        private readonly GameObject[] homeMarkers, awayMarkers;
        private readonly float[] gaitPhases;
        private readonly FootballConfig config;
        private float previousTime;
        public Transform[] PlayerTransforms { get; }

        private FootballPlayerVisuals(FootballConfig settings)
        {
            config = settings;
            int count = FootballMatch.PlayersPerTeam * 2;
            PlayerTransforms = new Transform[count];
            bodies = new Transform[count]; leftLegs = new Transform[count]; rightLegs = new Transform[count];
            leftArms = new Transform[count]; rightArms = new Transform[count];
            homeMarkers = new GameObject[count]; awayMarkers = new GameObject[count];
            gaitPhases = new float[count];
        }

        public static FootballPlayerVisuals Build(Transform root, RuntimeAssets assets, FootballConfig config, TMP_FontAsset font)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            if (assets == null) throw new ArgumentNullException(nameof(assets));
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (font == null) throw new ArgumentNullException(nameof(font));
            config.ValidateOrThrow();
            var result = new FootballPlayerVisuals(config);
            Material homeShirt = Material(assets, config.HomeColor), awayShirt = Material(assets, config.AwayColor);
            Material homeKeeper = Material(assets, config.KeeperHomeColor), awayKeeper = Material(assets, config.KeeperAwayColor);
            Material homeShorts = Material(assets, config.HomeShortsColor), awayShorts = Material(assets, config.AwayShortsColor);
            Material socks = Material(assets, config.SocksColor), boots = Material(assets, config.BootsColor);
            Material hair = Material(assets, config.HairColor);
            Material homeSelection = Material(assets, config.SelectedColor), awaySelection = Material(assets, config.AwaySelectedColor);
            var skins = new Material[config.SkinColors.Length];
            for (int i = 0; i < skins.Length; i++) skins[i] = Material(assets, config.SkinColors[i]);
            for (int i = 0; i < result.PlayerTransforms.Length; i++)
            {
                int team = i / FootballMatch.PlayersPerTeam;
                int number = i % FootballMatch.PlayersPerTeam + 1;
                bool keeper = number == 1;
                var player = new GameObject((team == 0 ? config.HomeName : config.AwayName) + " " + number);
                player.transform.SetParent(root, false);
                result.PlayerTransforms[i] = player.transform;
                var body = new GameObject("Procedural Footballer");
                body.transform.SetParent(player.transform, false);
                result.bodies[i] = body.transform;
                Material shirt = keeper ? (team == 0 ? homeKeeper : awayKeeper) : (team == 0 ? homeShirt : awayShirt);
                Material shorts = team == 0 ? homeShorts : awayShorts;
                Material skin = skins[i % skins.Length];
                result.BuildBody(i, body.transform, shirt, shorts, skin, socks, boots, hair);
                result.homeMarkers[i] = Selection("P1 Selection", player.transform, homeSelection, config);
                result.awayMarkers[i] = Selection("P2 Selection", player.transform, awaySelection, config);
                result.homeMarkers[i].SetActive(false); result.awayMarkers[i].SetActive(false);
                var labelObject = new GameObject("Player Number");
                labelObject.transform.SetParent(player.transform, false);
                labelObject.transform.localPosition = Vector3.up * config.LabelHeight;
                labelObject.transform.localRotation = Quaternion.Euler(0f, config.LabelYaw, 0f);
                TextMeshPro label = labelObject.AddComponent<TextMeshPro>();
                label.font = font;
                label.text = number.ToString(System.Globalization.CultureInfo.InvariantCulture);
                label.fontSize = config.LabelFontSize;
                label.color = config.LabelColor;
                label.alignment = TextAlignmentOptions.Center;
                label.rectTransform.sizeDelta = new Vector2(config.LabelWidth, config.LabelBoxHeight);
                label.raycastTarget = false;
            }
            return result;
        }

        private void BuildBody(int index, Transform body, Material shirt, Material shorts, Material skin,
            Material socks, Material boots, Material hair)
        {
            float height = config.PlayerHeight;
            float hip = height * config.LegLengthFraction;
            float torsoHeight = height * config.TorsoHeightFraction;
            float torsoWidth = height * config.TorsoWidthFraction;
            float torsoDepth = height * config.TorsoDepthFraction;
            Cube("Shirt", body, new Vector3(0f, hip + torsoHeight * 0.5f, 0f),
                new Vector3(torsoWidth, torsoHeight, torsoDepth), shirt);
            Cube("Original Chest Band", body, new Vector3(0f, hip + torsoHeight * 0.7f, 0f),
                new Vector3(torsoWidth, height * config.ShirtBandHeightFraction, torsoDepth + config.SelectionElevation), shorts);
            Cube("Shorts", body, new Vector3(0f, hip, 0f),
                new Vector3(torsoWidth, height * config.ShortsHeightFraction, torsoDepth), shorts);
            Cube("Neck", body, new Vector3(0f, hip + torsoHeight, 0f),
                new Vector3(config.HeadRadius, height * config.NeckHeightFraction, config.HeadRadius), skin);
            float headY = height - config.HeadRadius;
            Primitive("Head", PrimitiveType.Sphere, body, new Vector3(0f, headY, 0f),
                Vector3.one * (config.HeadRadius * 2f), skin);
            Cube("Hair", body, new Vector3(0f, height - height * config.HairHeightFraction * 0.5f, 0f),
                new Vector3(config.HeadRadius * 1.65f, height * config.HairHeightFraction, config.HeadRadius * 1.65f), hair);
            leftLegs[index] = BuildLeg("Left Leg", body, -height * config.LegSpacingFraction * 0.5f, hip, skin, socks, boots);
            rightLegs[index] = BuildLeg("Right Leg", body, height * config.LegSpacingFraction * 0.5f, hip, skin, socks, boots);
            leftArms[index] = BuildArm("Left Arm", body, -1f, hip + torsoHeight, torsoWidth, skin, shirt);
            rightArms[index] = BuildArm("Right Arm", body, 1f, hip + torsoHeight, torsoWidth, skin, shirt);
        }

        private Transform BuildLeg(string name, Transform body, float x, float hip, Material skin, Material socks, Material boots)
        {
            var pivot = new GameObject(name);
            pivot.transform.SetParent(body, false);
            pivot.transform.localPosition = new Vector3(x, hip, 0f);
            float width = config.PlayerHeight * config.LegWidthFraction;
            Cube("Leg", pivot.transform, new Vector3(0f, -hip * 0.5f, 0f), new Vector3(width, hip, width), skin);
            float sockLength = config.PlayerHeight * config.SockLengthFraction;
            Cube("Sock", pivot.transform, new Vector3(0f, -hip + sockLength * 0.5f, 0f),
                new Vector3(width, sockLength, width), socks);
            float bootHeight = config.PlayerHeight * config.BootHeightFraction;
            float bootLength = config.PlayerHeight * config.BootLengthFraction;
            Cube("Boot", pivot.transform, new Vector3(0f, -hip + bootHeight * 0.5f, (bootLength - width) * 0.5f),
                new Vector3(width, bootHeight, bootLength), boots);
            return pivot.transform;
        }

        private Transform BuildArm(string name, Transform body, float sign, float shoulder, float torsoWidth,
            Material skin, Material shirt)
        {
            var pivot = new GameObject(name);
            pivot.transform.SetParent(body, false);
            float length = config.PlayerHeight * config.ArmLengthFraction;
            float width = config.PlayerHeight * config.ArmWidthFraction;
            pivot.transform.localPosition = new Vector3(sign * (torsoWidth + width) * 0.5f, shoulder, 0f);
            pivot.transform.localRotation = Quaternion.Euler(0f, 0f, sign * config.ArmSpreadDegrees);
            Cube("Arm", pivot.transform, new Vector3(0f, -length * 0.5f, 0f), new Vector3(width, length, width), skin);
            Cube("Sleeve", pivot.transform, new Vector3(0f, -length * 0.25f, 0f), new Vector3(width, length * 0.5f, width), shirt);
            return pivot.transform;
        }

        public void Render(FootballPlayer[] players, int selectedHome, int selectedAway, bool twoPlayers, float time)
        {
            float delta = Mathf.Max(0f, time - previousTime);
            previousTime = time;
            for (int i = 0; i < PlayerTransforms.Length; i++)
            {
                FootballPlayer player = players[i];
                Vector3 facing = new Vector3(player.Facing.X, 0f, player.Facing.Z);
                if (facing.sqrMagnitude > 0f)
                    bodies[i].localRotation = Quaternion.RotateTowards(bodies[i].localRotation,
                        Quaternion.LookRotation(facing, Vector3.up), config.BodyTurnDegreesPerSecond * delta);
                float speed = Mathf.Sqrt(player.Velocity.X * player.Velocity.X + player.Velocity.Z * player.Velocity.Z);
                float weight = Mathf.Clamp01(speed / config.Gameplay.JogSpeed);
                float stride = Mathf.Repeat(gaitPhases[i] + delta * speed * config.StrideRadiansPerMetre, Mathf.PI * 2f);
                gaitPhases[i] = stride;
                float swing = Mathf.Sin(stride) * weight;
                leftLegs[i].localRotation = Quaternion.Euler(swing * config.LegSwingDegrees, 0f, 0f);
                rightLegs[i].localRotation = Quaternion.Euler(-swing * config.LegSwingDegrees, 0f, 0f);
                leftArms[i].localRotation = Quaternion.Euler(-swing * config.ArmSwingDegrees, 0f, -config.ArmSpreadDegrees);
                rightArms[i].localRotation = Quaternion.Euler(swing * config.ArmSwingDegrees, 0f, config.ArmSpreadDegrees);
                bodies[i].localPosition = Vector3.up * (Mathf.Abs(Mathf.Sin(stride)) * config.BodyBobMetres * weight);
                bool home = i == selectedHome;
                bool away = twoPlayers && i == selectedAway;
                if (homeMarkers[i].activeSelf != home) homeMarkers[i].SetActive(home);
                if (awayMarkers[i].activeSelf != away) awayMarkers[i].SetActive(away);
            }
        }

        private static GameObject Selection(string name, Transform parent, Material material, FootballConfig config)
        {
            var marker = new GameObject(name);
            marker.transform.SetParent(parent, false);
            LineRenderer line = marker.AddComponent<LineRenderer>();
            line.useWorldSpace = false; line.loop = true;
            line.sharedMaterial = material; line.widthMultiplier = config.SelectionLineWidth;
            line.positionCount = config.SelectionSegments;
            for (int i = 0; i < config.SelectionSegments; i++)
            {
                float angle = i * Mathf.PI * 2f / config.SelectionSegments;
                line.SetPosition(i, new Vector3(Mathf.Cos(angle) * config.SelectionRadius,
                    config.SelectionElevation, Mathf.Sin(angle) * config.SelectionRadius));
            }
            return marker;
        }

        private static void Cube(string name, Transform parent, Vector3 position, Vector3 scale, Material material) =>
            Primitive(name, PrimitiveType.Cube, parent, position, scale, material);

        private static Material Material(RuntimeAssets assets, Color color)
        {
            Material material = assets.Material(color);
            material.enableInstancing = true;
            return material;
        }

        private static void Primitive(string name, PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            Collider collider = part.GetComponent<Collider>();
            collider.enabled = false;
            UnityEngine.Object.Destroy(collider);
            part.GetComponent<MeshRenderer>().sharedMaterial = material;
        }
    }
}
