﻿using Mobsub.SubtitleParse.AssTypes;
using System;
using System.Collections.Generic;
using System.Text;

namespace Mobsub.SubtitleParse.AssUtils;

public readonly struct AssStyleSnapshot(AssStyle s)
{
    public AssColor32 PrimaryColorWithAlpha { get; init; } = s.PrimaryColour;
    public AssColor32 SecondaryColorWithAlpha { get; init; } = s.SecondaryColour;
    public AssColor32 BorderColorWithAlpha { get; init; } = s.OutlineColour;
    public AssColor32 ShadowColorWithAlpha { get; init; } = s.BackColour;
    public byte Alignment { get; init; } = s.Alignment;
    public double BlueEdges { get; init; } = 0;
    public double BlurEdgesGaussian { get; init; } = 0;
    public double BorderX { get; init; } = s.Outline;
    public double BorderY { get; init; } = s.Outline;
    public int Bold { get; init; } = s.Bold ? 1 : 0;
    // clip, iclip, fade, fad
    public double FontShiftX { get; init; } = 0;
    public double FontShiftY { get; init; } = 0;
    public double FontEncoding { get; init; } = s.Encoding;
    public string Fontname { get; init; } = s.Fontname;
    public double FontRotationX { get; init; } = 0;
    public double FontRotationY { get; init; } = 0;
    public double FontRotationZ { get; init; } = 0;
    public double FontScaleX { get; init; } = s.ScaleX;
    public double FontScaleY { get; init; } = s.ScaleY;
    public double FontSpacing { get; init; } = s.Spacing;
    public double Fontsize { get; init; } = s.Fontsize;
    public bool Italic { get; init; } = s.Italic;
    // ko, kf, K, k
    // move, org, pos, pbo, p, q
    // r
    public double ShadowX { get; init; } = s.Shadow;
    public double ShadowY { get; init; } = s.Shadow;
    public bool StrikeOut { get; init; } = s.StrikeOut;
    // t
    public bool Underline { get; init; } = s.Underline;
}
