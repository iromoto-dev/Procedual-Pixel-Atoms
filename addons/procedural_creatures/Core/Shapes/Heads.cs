// Procedural Pixel Creatures - head shape block: cranium, snout, jaw with mouth interior and teeth,
// brow, cheeks, eyes and nostrils. Ears and horns are separate blocks that attach to the head bone.

#nullable enable

using System;
using PixelCreatures.Core.Anatomy;

namespace PixelCreatures.Core.Shapes
{
    public enum TeethKind : byte
    {
        None,
        Small,
        Fangs,
        Tusks,
        Sabre,
        Beak,
        Mandibles,
    }

    /// <summary>Nose shapes: animal nose pad at the snout tip or humanoid noses on a flat face.</summary>
    public enum NoseKind : byte
    {
        Pad,
        /// <summary>Bulbous round nose (dwarves, ogres).</summary>
        Round,
        /// <summary>Long, slightly hooked nose (goblins, trolls).</summary>
        Long,
        /// <summary>Broad flat nose with visible nostrils (orcs, apes).</summary>
        Flat,
    }

    public sealed class HeadSpec
    {
        public string Name = "head";
        public int ParentBone;
        /// <summary>Head joint (atlas) in creature space.</summary>
        public Vec3 Position;
        /// <summary>Direction the skull points (creature space).</summary>
        public Vec3 Forward = Vec3.UnitX;
        public double SkullLength = 8, SkullHeight = 6.5, SkullWidth = 6;
        /// <summary>Skull centre offset along forward as a fraction of skull length.</summary>
        public double SkullOffset = 0.3;
        public double SnoutLength = 5, SnoutRadius = 2.6, SnoutTaper = 0.7;
        /// <summary>Snout droop in radians (positive = points down).</summary>
        public double SnoutDrop = 0.2;
        /// <summary>Vertical placement of the snout base as fraction of skull height (negative = lower).</summary>
        public double SnoutHeight = -0.12;
        public double SnoutWidth = 1.0;
        public double NoseSize = 1.0;
        public NoseKind Nose = NoseKind.Pad;
        public bool DarkNose = true;
        public double JawLength = 0.9, JawRadius = 1.6, JawDepth = 0.8;
        public double Brow;
        public double Cheeks;
        public double EyeSize = 2;
        public EyeStyle EyeStyle = EyeStyle.Bead;
        /// <summary>0 = eyes on the sides (prey), 1 = eyes facing forward (predator).</summary>
        public double EyeForward = 0.4;
        /// <summary>Eye height on the skull (-1 low .. 1 high).</summary>
        public double EyeHeight = 0.25;
        public double EyeAspect = 1.0;
        public int EyeCount = 2;
        public TeethKind Teeth = TeethKind.Small;
        public double TeethSize = 1;
        public int Group = -1;
        public double BlendRadius = 2.0;
        public MaterialSlot Material = MaterialSlot.Primary;
        public MaterialSlot JawMaterial = MaterialSlot.Primary;
        /// <summary>Material of snout and nose (Accent for beaks).</summary>
        public MaterialSlot SnoutMaterial = MaterialSlot.Primary;
        /// <summary>Snout keeps the body pattern (false for beaks and bare faces).</summary>
        public bool SnoutPattern = true;
        public double DomainLength = 12;
        public bool Nostrils = true;
        public double JawOpenMax = 0.55;
        /// <summary>High density facial structure (nasal bridge, cheekbones, brow over each eye, lips, chin).</summary>
        public bool Structure = true;
    }

    public sealed class HeadResult
    {
        public int Head, Jaw = -1;
        public int Group, JawGroup = -1;
        public Vec3 SkullCenterLocal;
        public Vec3 SkullRadii;
        public Vec3 SnoutTipLocal;
        public double TotalLength;
        /// <summary>Jaw bone length and radius at the hinge (0 without a jaw); chin position in the head frame.</summary>
        public double JawLength, JawRadius;
        public Vec3 ChinLocal;
    }

    public static class Heads
    {
        public static HeadResult Build(AnatomyBuilder b, HeadSpec s)
        {
            var r = new HeadResult();
            int group = s.Group >= 0 ? s.Group : b.Group(s.Name, s.BlendRadius);
            r.Group = group;
            Vec3 fwd = s.Forward.NormalizedOr(Vec3.UnitX);
            int head = b.BoneAlong(s.Name, s.ParentBone, s.Position, fwd, Vec3.UnitY, BoneRole.Head, 0, s.SkullLength);
            r.Head = head;

            // --- cranium (head bone frame: +X forward, +Y up, +Z right)
            double sl = s.SkullLength, sh = s.SkullHeight, sw = s.SkullWidth;
            Vec3 skullC = new Vec3(sl * s.SkullOffset, sh * 0.08, 0);
            Vec3 skullR = new Vec3(sl * 0.5, sh * 0.5, sw * 0.5);
            r.SkullCenterLocal = skullC;
            r.SkullRadii = skullR;
            double total = s.DomainLength;
            double snoutStartT = 0.55;
            var cranium = b.Ellipsoid(group, head, skullC, skullR, s.Material, PatternDomain.Head, 0.0, snoutStartT);
            cranium.DomainLength = total;
            cranium.Tag = s.Name + ".cranium";

            // --- snout / muzzle
            Vec3 snoutBase = skullC + new Vec3(skullR.X * 0.45, sh * s.SnoutHeight, 0);
            Vec3 snoutDir = ShapeMath.Pitch(Vec3.UnitX, -s.SnoutDrop);
            Vec3 snoutTip = snoutBase + snoutDir * s.SnoutLength;
            r.SnoutTipLocal = snoutTip;
            double snoutR0 = s.SnoutRadius, snoutR1 = Math.Max(0.6, s.SnoutRadius * s.SnoutTaper);
            if (s.SnoutLength > 0.6)
            {
                var snout = b.Cone(group, head, snoutBase, snoutR0, head, snoutTip, snoutR1, s.SnoutMaterial, PatternDomain.Head, snoutStartT, 1.0);
                snout.DomainLength = total;
                snout.Tag = s.Name + ".snout";
                if (!s.SnoutPattern) snout.Flags |= PrimFlags.NoPattern;
                if (s.SnoutWidth > 1.08)
                {
                    // broad muzzles: add side mass so the snout reads wide in front views
                    var wide = b.Ellipsoid(group, head, Vec3.Lerp(snoutBase, snoutTip, 0.55),
                        new Vec3(s.SnoutLength * 0.45, snoutR1 * 1.02, snoutR0 * s.SnoutWidth), s.SnoutMaterial, PatternDomain.Head, 0.7, 0.95);
                    wide.DomainLength = total;
                    if (!s.SnoutPattern) wide.Flags |= PrimFlags.NoPattern;
                }
            }
            // nose: animal pad at the snout tip or a humanoid nose on the face
            if (s.NoseSize > 0.1)
            {
                double ns = s.NoseSize;
                // humanoid noses sit on the face front (short snouts end inside the skull)
                Vec3 face = new Vec3(Math.Max(snoutTip.X + snoutR1 * 0.2, skullC.X + skullR.X * 0.9), snoutTip.Y + snoutR1 * 0.5, 0);
                switch (s.Nose)
                {
                    case NoseKind.Round:
                    {
                        Vec3 at = face + new Vec3(0.2 + 0.2 * ns, -0.1, 0);
                        var np = b.Ellipsoid(group, head, at, new Vec3(0.55 + 0.42 * ns, 0.5 + 0.4 * ns, 0.5 + 0.42 * ns), s.SnoutMaterial, PatternDomain.Head, 0.98, 1.0);
                        np.Flags |= PrimFlags.NoPattern;
                        np.DomainLength = total;
                        np.Tag = s.Name + ".nose";
                        break;
                    }
                    case NoseKind.Long:
                    {
                        // starts between the eyes, runs forward and down, hooks at the tip
                        Vec3 a = face + new Vec3(-0.6, 0.35 + 0.25 * ns, 0);
                        double nl = 0.9 + 1.5 * ns;
                        Vec3 mid = a + new Vec3(nl * 0.75, -nl * 0.3, 0);
                        Vec3 tip = mid + new Vec3(nl * 0.3, -nl * 0.32, 0);
                        double r0 = 0.45 + 0.28 * ns;
                        var n1 = b.Cone(group, head, a, r0, head, mid, r0 * 0.85, s.SnoutMaterial, PatternDomain.Head, 0.9, 1.0);
                        var n2 = b.Cone(group, head, mid, r0 * 0.85, head, tip, Math.Max(0.4, r0 * 0.55), s.SnoutMaterial, PatternDomain.Head, 0.95, 1.0);
                        foreach (var np in new[] { n1, n2 }) { np.Flags |= PrimFlags.NoPattern; np.DomainLength = total; np.Tag = s.Name + ".nose"; }
                        break;
                    }
                    case NoseKind.Flat:
                    {
                        Vec3 at = face + new Vec3(0.1 + 0.12 * ns, -0.15, 0);
                        var np = b.Ellipsoid(group, head, at, new Vec3(0.4 + 0.3 * ns, 0.45 + 0.35 * ns, 0.6 + 0.5 * ns), s.SnoutMaterial, PatternDomain.Head, 0.98, 1.0);
                        np.Flags |= PrimFlags.NoPattern;
                        np.DomainLength = total;
                        np.Tag = s.Name + ".nose";
                        if (ns > 0.9)
                        {
                            for (int side = -1; side <= 1; side += 2)
                                b.Feature(new FeatureDef
                                {
                                    Kind = FeatureKind.Nostril, Bone = head, LocalPosition = at + new Vec3(0.35 + 0.3 * ns, -0.15, side * (0.25 + 0.25 * ns)),
                                    LocalNormal = new Vec3(0.9, -0.1, side * 0.4).Normalized(), Size = 1, Side = side, Group = group,
                                });
                        }
                        break;
                    }
                    default:
                    {
                        Vec3 nose = snoutTip + new Vec3(snoutR1 * 0.55, snoutR1 * 0.35, 0);
                        var np = b.Ellipsoid(group, head, nose, new Vec3(snoutR1 * 0.55 * ns, snoutR1 * 0.5 * ns, snoutR1 * 0.8 * ns),
                            s.SnoutMaterial, PatternDomain.Head, 0.98, 1.0);
                        np.ShadeBias = s.DarkNose ? -2 : 0;
                        np.Flags |= PrimFlags.NoPattern;
                        np.DomainLength = total;
                        np.Tag = s.Name + ".nose";
                        break;
                    }
                }
            }
            // brow ridge
            if (s.Brow > 0.08)
            {
                Vec3 browC = skullC + new Vec3(skullR.X * 0.45, skullR.Y * 0.55, 0);
                var brow = b.Ellipsoid(group, head, browC, new Vec3(skullR.X * 0.42, skullR.Y * 0.28 * (0.6 + s.Brow), skullR.Z * 0.95),
                    s.Material, PatternDomain.Head, 0.3, 0.55);
                brow.DomainLength = total;
                brow.Tag = s.Name + ".brow";
            }
            // cheeks
            if (s.Cheeks > 0.08)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    Vec3 cc = skullC + new Vec3(skullR.X * 0.1, -skullR.Y * 0.35, side * skullR.Z * 0.55);
                    var ch = b.Ellipsoid(group, head, cc, new Vec3(skullR.X * 0.45, skullR.Y * 0.42 * (0.6 + s.Cheeks * 0.6), skullR.Z * 0.42 * (0.7 + s.Cheeks * 0.5)),
                        s.Material, PatternDomain.Head, 0.35, 0.6, null, side);
                    ch.DomainLength = total;
                    ch.Tag = s.Name + ".cheek";
                }
            }

            // --- jaw (own group so the mouth line reads) with mouth interior and teeth
            if (s.JawLength > 0.05)
            {
                int jawGroup = b.Group(s.Name + ".jaw", 1.2);
                r.JawGroup = jawGroup;
                Vec3 hingeLocal = skullC + new Vec3(-skullR.X * 0.25, -skullR.Y * 0.55, 0);
                Vec3 hingeWorld = b.RestWorld(head).Point(hingeLocal);
                Vec3 chinLocal = snoutTip + new Vec3(-snoutR1 * 0.6 - s.SnoutLength * (1 - s.JawLength) * 0.8, -snoutR1 * 0.75 * s.JawDepth, 0);
                Vec3 jawDirWorld = b.RestWorld(head).Dir(chinLocal - hingeLocal);
                int jaw = b.BoneAlong(s.Name + ".jaw", head, hingeWorld, jawDirWorld, b.RestWorld(head).Dir(Vec3.UnitY), BoneRole.Jaw, 0, (chinLocal - hingeLocal).Length);
                r.Jaw = jaw;
                double jl = (chinLocal - hingeLocal).Length;
                double jr0 = s.JawRadius, jr1 = Math.Max(0.55, s.JawRadius * 0.72);
                r.JawLength = jl; r.JawRadius = jr0; r.ChinLocal = chinLocal;
                var jawP = b.Cone(jawGroup, jaw, new Vec3(jl * 0.08, 0, 0), jr0, jaw, new Vec3(jl, 0, 0), jr1, s.JawMaterial, PatternDomain.Head, 0.45, 0.95);
                jawP.DomainLength = total;
                jawP.Tag = s.Name + ".jawbone";

                // mouth interior: sits inside the head, revealed only when the jaw opens
                int mouthGroup = b.Group(s.Name + ".mouth", 0.8, 0, GroupFlags.NoOutline | GroupFlags.NoContour | GroupFlags.Interior, -1.5);
                Vec3 mouthC = Vec3.Lerp(hingeLocal, chinLocal, 0.6) + new Vec3(0, jr0 * 0.7, 0);
                var mouth = b.Ellipsoid(mouthGroup, head, mouthC, new Vec3(jl * 0.5, Math.Max(0.8, jr0 * 0.9), Math.Max(0.8, s.SkullWidth * 0.28)), MaterialSlot.Mouth);
                mouth.Flags |= PrimFlags.NoShadow | PrimFlags.NoPattern;
                mouth.Tag = s.Name + ".mouthInterior";

                AddTeeth(b, s, head, jaw, jl, jr0, jr1, snoutTip, snoutR1, jawGroup, group);
            }

            if (s.Structure) BuildStructure(b, s, head, group, skullC, skullR, snoutBase, snoutTip, snoutR0, snoutR1, total, r);

            // --- eyes
            double eyeT = DMath.Lerp(0.15, 0.62, s.EyeForward);
            for (int side = -1; side <= 1; side += 2)
            {
                if (s.EyeCount <= 0 || s.EyeSize <= 0) break;
                if (s.EyeCount == 1 && side == -1) continue;
                double lat = s.EyeCount == 1 ? 0 : side * DMath.Lerp(0.95, 0.55, s.EyeForward);
                Vec3 dir = new Vec3(DMath.Lerp(0.35, 0.72, s.EyeForward), DMath.Clamp(s.EyeHeight, -0.8, 0.9) * 0.55 + 0.15, lat);
                var basis = Mat3.Identity;
                Vec3 surf = ShapeMath.EllipsoidSurface(skullC, basis, skullR, dir);
                Vec3 nrm = ShapeMath.EllipsoidNormal(basis, skullR, surf - skullC);
                if (s.EyeCount == 1) { surf = skullC + new Vec3(skullR.X * 0.85, skullR.Y * 0.2, 0); nrm = new Vec3(1, 0.15, 0).Normalized(); }
                b.Feature(new FeatureDef
                {
                    Kind = FeatureKind.Eye,
                    Bone = head,
                    LocalPosition = surf,
                    LocalNormal = nrm,
                    Size = s.EyeSize,
                    Style = s.EyeStyle,
                    Aspect = s.EyeAspect,
                    Brow = s.Brow,
                    Side = s.EyeCount == 1 ? 0 : side,
                    Group = group,
                });
                _ = eyeT;
            }
            // --- nostrils
            if (s.Nostrils && s.SnoutLength > 2.5 && snoutR1 > 1.1)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    Vec3 np = snoutTip + new Vec3(snoutR1 * 0.9, snoutR1 * 0.45, side * snoutR1 * 0.45);
                    b.Feature(new FeatureDef { Kind = FeatureKind.Nostril, Bone = head, LocalPosition = np, LocalNormal = new Vec3(0.8, 0.3, side * 0.5).Normalized(), Size = 1, Side = side, Group = group });
                }
            }
            r.TotalLength = s.SkullLength * (1 - s.SkullOffset) + s.SnoutLength;
            b.Rig.Head = head;
            if (r.Jaw >= 0) b.Rig.Jaw = r.Jaw;
            b.Rig.JawOpenMax = s.JawOpenMax;
            return r;
        }

        /// <summary>
        /// High density facial structure, blended into the head group: the stop and nasal bridge between the
        /// eyes and the snout, cheekbones below the eyes, a brow arch over each eye, lips (flews) along the lower
        /// snout and a chin. These primitives only appear at a pixel density of at least Limbs.HdDensity.
        /// </summary>
        private static void BuildStructure(AnatomyBuilder b, HeadSpec s, int head, int group, Vec3 skullC, Vec3 skullR,
            Vec3 snoutBase, Vec3 snoutTip, double snoutR0, double snoutR1, double total, HeadResult r)
        {
            double hd = Limbs.HdDensity;
            PrimitiveDef Hd(PrimitiveDef p, string tag, bool pattern = true)
            {
                p.MinDensity = hd;
                p.DomainLength = total;
                p.Tag = s.Name + "." + tag;
                if (!pattern) p.Flags |= PrimFlags.NoPattern;
                return p;
            }
            bool snout = s.SnoutLength > 1.5 && s.Nose == NoseKind.Pad;
            if (snout)
            {
                Vec3 dir = (snoutTip - snoutBase).NormalizedOr(Vec3.UnitX);
                Vec3 up = new Vec3(-dir.Y, dir.X, 0);
                // nasal bridge: a narrow ridge along the top of the snout, rising into the stop at the forehead
                Vec3 b0 = snoutBase + up * (snoutR0 * 0.55) - dir * (snoutR0 * 0.2);
                Vec3 b1 = snoutTip + up * (snoutR1 * 0.45) - dir * (snoutR1 * 0.5);
                Hd(b.Cone(group, head, b0, snoutR0 * 0.62, head, b1, snoutR1 * 0.55, s.SnoutMaterial, PatternDomain.Head, 0.6, 0.95), "bridge", s.SnoutPattern);
                Vec3 stop = skullC + new Vec3(skullR.X * 0.55, skullR.Y * 0.25, 0);
                Hd(b.Ellipsoid(group, head, stop, new Vec3(skullR.X * 0.38, skullR.Y * 0.42, skullR.Z * 0.55), s.Material, PatternDomain.Head, 0.45, 0.6), "stop");
                // lips / flews: fuller lower snout on both sides
                for (int side = -1; side <= 1; side += 2)
                {
                    Vec3 l0 = Vec3.Lerp(snoutBase, snoutTip, 0.25) - up * (snoutR0 * 0.35) + new Vec3(0, 0, side * snoutR0 * 0.42);
                    Vec3 l1 = Vec3.Lerp(snoutBase, snoutTip, 0.9) - up * (snoutR1 * 0.38) + new Vec3(0, 0, side * snoutR1 * 0.4);
                    Hd(b.Cone(group, head, l0, snoutR0 * 0.62, head, l1, snoutR1 * 0.58, s.SnoutMaterial, PatternDomain.Head, 0.7, 1.0, side), "lip", s.SnoutPattern);
                }
            }
            // cheekbones under and behind the eyes; brow arches over them
            for (int side = -1; side <= 1; side += 2)
            {
                if (s.EyeCount < 2 && side < 0) continue;
                double lat = side * DMath.Lerp(0.95, 0.55, s.EyeForward);
                Vec3 dir = new Vec3(DMath.Lerp(0.35, 0.72, s.EyeForward), DMath.Clamp(s.EyeHeight, -0.8, 0.9) * 0.55 + 0.15, s.EyeCount == 1 ? 0 : lat);
                Vec3 eye = ShapeMath.EllipsoidSurface(skullC, Mat3.Identity, skullR, dir);
                Vec3 nrm = ShapeMath.EllipsoidNormal(Mat3.Identity, skullR, eye - skullC);
                double er = Math.Max(0.8, s.EyeSize * 0.55);
                Vec3 cheek = eye - nrm * (er * 0.35) + new Vec3(-er * 0.4, -er * 1.25, 0);
                Hd(b.Ellipsoid(group, head, cheek, new Vec3(skullR.X * 0.36, er * 0.75, skullR.Z * 0.32), s.Material, PatternDomain.Head, 0.4, 0.55, null, side), "cheekbone");
                double browH = 0.35 + 0.65 * DMath.Saturate(s.Brow + 0.25);
                Vec3 brow = eye - nrm * (er * 0.25) + new Vec3(er * 0.15, er * (0.95 + 0.25 * browH), 0);
                Hd(b.Ellipsoid(group, head, brow, new Vec3(er * 1.15, er * 0.38 * browH + 0.3, er * 0.75), s.Material, PatternDomain.Head, 0.4, 0.5, null, side), "browarch");
            }
            if (r.Jaw >= 0 || s.JawLength > 0.05)
            {
                // chin: a small rounded mass under the front of the lower jaw (on the jaw bone, so it moves)
                if (r.Jaw >= 0)
                {
                    double jl = r.JawLength, jr = r.JawRadius;
                    Hd(b.Ellipsoid(r.JawGroup, r.Jaw, new Vec3(jl * 0.82, -jr * 0.35, 0), new Vec3(jl * 0.22, jr * 0.62, jr * 0.75), s.JawMaterial, PatternDomain.Head, 0.85, 0.95), "chin");
                }
            }
        }

        private static void AddTeeth(AnatomyBuilder b, HeadSpec s, int head, int jaw, double jl, double jr0, double jr1, Vec3 snoutTip, double snoutR1, int jawGroup, int headGroup)
        {
            double ts = s.TeethSize;
            switch (s.Teeth)
            {
                case TeethKind.Small:
                case TeethKind.Fangs:
                case TeethKind.Sabre:
                {
                    // upper teeth row along the snout edge (hidden inside the lip until the mouth opens)
                    int tg = b.Group(s.Name + ".teeth", 0.4, 0, GroupFlags.NoContour, -0.6);
                    int count = Math.Max(2, (int)Math.Round(jl / 2.2));
                    for (int i = 0; i < count; i++)
                    {
                        double t = 0.35 + 0.6 * i / Math.Max(1, count - 1);
                        Vec3 top = Vec3.Lerp(snoutTip + new Vec3(-jl * 0.9, 0, 0), snoutTip, t) + new Vec3(0, -snoutR1 * 0.55, 0);
                        for (int side = -1; side <= 1; side += 2)
                        {
                            Vec3 a = top + new Vec3(0, 0, side * snoutR1 * 0.55);
                            var tooth = b.Cone(tg, head, a, 0.5 * ts, head, a + new Vec3(0.1, -0.9 * ts, 0), 0.3, MaterialSlot.Teeth);
                            tooth.Flags |= PrimFlags.Hard | PrimFlags.NoPattern | PrimFlags.NoShadow | PrimFlags.Thin;
                            tooth.MinQuality = 1;
                        }
                    }
                    if (s.Teeth == TeethKind.Fangs || s.Teeth == TeethKind.Sabre)
                    {
                        double len = s.Teeth == TeethKind.Sabre ? 3.2 * ts : 1.8 * ts;
                        int fg = b.Group(s.Name + ".fangs", 0.4, 0, GroupFlags.None, 0.3);
                        for (int side = -1; side <= 1; side += 2)
                        {
                            Vec3 a = snoutTip + new Vec3(-snoutR1 * 0.9, -snoutR1 * 0.45, side * snoutR1 * 0.55);
                            var fang = b.Cone(fg, head, a, 0.65 * ts, head, a + new Vec3(0.25 * ts, -len, 0), 0.3, MaterialSlot.Teeth);
                            fang.Flags |= PrimFlags.Hard | PrimFlags.NoPattern | PrimFlags.Thin | PrimFlags.NoShadow;
                        }
                    }
                    break;
                }
                case TeethKind.Tusks:
                {
                    int tg = b.Group(s.Name + ".tusks", 0.5, 0, GroupFlags.None, 0.4);
                    for (int side = -1; side <= 1; side += 2)
                    {
                        Vec3 a = new Vec3(jl * 0.72, jr1 * 0.5, side * jr1 * 0.9);
                        Vec3 mid = a + new Vec3(0.9 * ts, 1.6 * ts, side * 0.6 * ts);
                        Vec3 tip = mid + new Vec3(-0.2 * ts, 1.4 * ts, side * 0.2 * ts);
                        var t1 = b.Cone(tg, jaw, a, 0.8 * ts, jaw, mid, 0.6 * ts, MaterialSlot.Teeth);
                        var t2 = b.Cone(tg, jaw, mid, 0.6 * ts, jaw, tip, 0.3, MaterialSlot.Teeth);
                        t1.Flags |= PrimFlags.NoPattern | PrimFlags.NoShadow;
                        t2.Flags |= PrimFlags.NoPattern | PrimFlags.NoShadow | PrimFlags.Thin;
                    }
                    break;
                }
                case TeethKind.Beak:
                {
                    // hooked upper beak tip over the jaw
                    int bg = b.Group(s.Name + ".beak", 0.8, 0, GroupFlags.None, 0.3);
                    var hook = b.Cone(bg, head, snoutTip + new Vec3(-snoutR1 * 0.6, 0, 0), snoutR1 * 0.95, head, snoutTip + new Vec3(snoutR1 * 0.7, -snoutR1 * 0.9 * ts, 0), 0.4, MaterialSlot.Accent);
                    hook.Flags |= PrimFlags.NoPattern;
                    break;
                }
                case TeethKind.Mandibles:
                {
                    int mg = b.Group(s.Name + ".mandibles", 0.5, 0, GroupFlags.None, 0.3);
                    for (int side = -1; side <= 1; side += 2)
                    {
                        Vec3 a = snoutTip + new Vec3(-snoutR1 * 0.4, -snoutR1 * 0.3, side * snoutR1 * 0.8);
                        Vec3 m = a + new Vec3(1.6 * ts, -0.4 * ts, side * 0.9 * ts);
                        Vec3 tip = m + new Vec3(0.9 * ts, -0.2 * ts, -side * 1.1 * ts);
                        var m1 = b.Cone(mg, jaw, b.RestWorld(jaw).InversePoint(b.RestWorld(head).Point(a)), 0.8 * ts, jaw, b.RestWorld(jaw).InversePoint(b.RestWorld(head).Point(m)), 0.55 * ts, MaterialSlot.Accent);
                        var m2 = b.Cone(mg, jaw, b.RestWorld(jaw).InversePoint(b.RestWorld(head).Point(m)), 0.55 * ts, jaw, b.RestWorld(jaw).InversePoint(b.RestWorld(head).Point(tip)), 0.3, MaterialSlot.Accent);
                        m1.Flags |= PrimFlags.NoPattern; m2.Flags |= PrimFlags.NoPattern | PrimFlags.Thin;
                    }
                    break;
                }
            }
        }
    }
}
