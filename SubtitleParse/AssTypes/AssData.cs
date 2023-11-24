using System.Text;
using Mobsub.Utils;

namespace Mobsub.AssTypes;

public class AssData
{
    public bool CarriageReturn = false;
    public Encoding CharEncoding = DetectEncoding.EncodingRefOS();
    public HashSet<AssSection> Sections = [];
    public AssScriptInfo ScriptInfo {get; set;} = new AssScriptInfo(){};
    public AssStyles Styles { get; set; } = new AssStyles(){};
    public AssEvents Events {get; set;} = new AssEvents(){};
    public Dictionary<string, string?> AegisubProjectGarbage =  [];
    public List<string> AegiusbExtradata = [];
    public List<AssEmbeddedFont> Fonts = [];
    public List<AssEmbeddedGraphic> Graphics = [];
}
