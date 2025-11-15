namespace Mobsub.SubtitleParse.PGS.DataTypes;

public struct PresentationCompositionSegment
{
    //public SegmentHeader Header;
    
    /// <summary>
    /// Video width in pixels
    /// </summary>
    public ushort Width;
    
    /// <summary>
    /// Video height in pixels
    /// </summary>
    public ushort Height;
    
    /// <summary>
    /// Always 0x10
    /// </summary>
    public byte FrameRate;
    
    /// <summary>
    /// Number of this specific composition. It is incremented by one every time a graphics update occurs
    /// </summary>
    public ushort CompositionNumber;
    
    /// <summary>
    /// Type of this composition
    /// </summary>
    public CompositionType CompositionState;
    
    /// <summary>
    /// Indicates if this PCS describes a Palette only Display Update
    /// </summary>
    public PaletteUpdateFlag PaletteUpdateFlag;

    /// <summary>
    /// ID of the palette to be used in the Palette only Display Update
    /// </summary>
    public byte PaletteID;
    
    /// <summary>
    /// Number of composition objects defined in this segment
    /// </summary>
    public byte NumberOfCompositionObjects;
    
    public CompositionObject[]? compositionObjects;
}

public enum CompositionType : byte
{
    /// <summary>
    /// This defines a display update, and contains only functional segments with elements that are different from the preceding composition. It’s mostly used to stop displaying objects on the screen by defining a composition with no composition objects (a value of zero in the Number of Composition Objects flag) but also used to define a new composition with new objects and objects defined since the Epoch Start
    /// </summary>
    Normal = 0x00,
    /// <summary>
    /// This defines a display refresh. This is used to compose in the middle of the Epoch. It includes functional segments with new objects to be used in a new composition, replacing old objects with the same Object ID
    /// </summary>
    AcquisitionPoint = 0x40,
    /// <summary>
    /// This defines a new display. The Epoch Start contains all functional segments needed to display a new composition on the screen
    /// </summary>
    EpochStart = 0x80,
}

public enum PaletteUpdateFlag : byte
{
    False = 0x00,
    True = 0x80,
}

public struct CompositionObject
{
    /// <summary>
    /// ID of the ODS segment that defines the image to be shown
    /// </summary>
    public short ObjectID;
    /// <summary>
    /// Id of the WDS segment to which the image is allocated in the PCS. Up to two images may be assigned to one window
    /// </summary>
    public byte WindowID;
    /// <summary>
    /// Force display of the cropped image object. 0x40 is true, 0x00 is false (off)
    /// </summary>
    public byte ObjectCroppedFlag;
    /// <summary>
    /// X offset from the top left pixel of the image on the screen
    /// </summary>
    public ushort ObjectHorizontalPosition;
    /// <summary>
    /// Y offset from the top left pixel of the image on the screen
    /// </summary>
    public ushort ObjectVerticalPosition;
    /// <summary>
    /// X offset from the top left pixel of the cropped object in the screen. Only used when the Object Cropped Flag is set to 0x40.
    /// </summary>
    public ushort? ObjectCroppingHorizontalPosition;
    /// <summary>
    /// Y offset from the top left pixel of the cropped object in the screen. Only used when the Object Cropped Flag is set to 0x40.
    /// </summary>
    public ushort? ObjectCroppingVerticalPosition;
    /// <summary>
    /// Width of the cropped object in the screen. Only used when the Object Cropped Flag is set to 0x40.
    /// </summary>
    public ushort? ObjectCroppingWidth;
    /// <summary>
    /// Heightl of the cropped object in the screen. Only used when the Object Cropped Flag is set to 0x40.
    /// </summary>
    public ushort? ObjectCroppingHeight;
}