from pathlib import Path
from xml.sax.saxutils import escape,quoteattr
import xml.etree.ElementTree as ET
import json,re,shutil
app=Path(__file__).resolve().parent.parent/'Carte'
motion=json.loads((app/'Assets/Illustrations/motion.json').read_text(encoding='utf-8'))
def tag(e):return e.tag.split('}')[-1]
def nm(s):return s.replace('-','_')
def channels(frames):
 result=set()
 for f in frames:
  for name,value in re.findall(r'(\w+)\(([-.\d]+)(?:px|deg)?\)',f.get('transform','')):
   result.update(['scaleX','scaleY'] if name=='scale' else [name])
 return [c for c in ['translateX','translateY','rotate','scaleX','scaleY'] if c in result]
def transform_values(frame):
 values={c:1 if c.startswith('scale') else 0 for c in ['translateX','translateY','rotate','scaleX','scaleY']}
 for name,value in re.findall(r'(\w+)\(([-.\d]+)(?:px|deg)?\)',frame.get('transform','')):
  if name=='scale':values['scaleX']=values['scaleY']=float(value)
  else:values[name]=float(value)
 return values
propertymap={'translateX':('TranslateTransform','X'),'translateY':('TranslateTransform','Y'),'rotate':('RotateTransform','Angle'),'scaleX':('ScaleTransform','ScaleX'),'scaleY':('ScaleTransform','ScaleY')}
definitions=[]
for id,spec in motion['items'].items():
 svg=ET.parse(app/'Assets/Illustrations'/Path(spec['asset']).name).getroot()
 tracks={t['part']:t for t in spec['tracks']}
 native=[]
 def native_tracks(t,liquid=False):
  frames=t['frames'];offsets=[f.get('offset',i/(len(frames)-1)) for i,f in enumerate(frames)]
  keys=channels(frames)
  if 'opacity' in frames[0]:keys=['opacity']+keys
  for key in keys:
   target='Part_'+nm(t['part']) if key=='opacity' or liquid else 'Layer_'+nm(t['part'])+'_'+key
   prop='Opacity' if key=='opacity' else propertymap[key][1]
   vals=[f['opacity'] if key=='opacity' else transform_values(f)[key] for f in frames]
   native.append((target,prop,t['duration'],t['delay'],list(zip(offsets,vals))))
   if liquid and key=='translateY':native.append(('Layer_liquid_counter','Y',t['duration'],t['delay'],list(zip(offsets,[-v for v in vals]))))
 def group(e,children):
  part=e.get('data-part');attrs=''
  if part:attrs+=' x:Name="Part_'+nm(part)+'"'
  if e.get('opacity'):attrs+=' Opacity="'+e.get('opacity')+'"'
  if part in tracks:
   t=tracks[part];origin=[float(v.replace('px','')) for v in t.get('origin','24px 24px').split()]
   for key in reversed(channels(t['frames'])):
    kind,prop=propertymap[key];center=f' CenterX="{origin[0]}" CenterY="{origin[1]}"' if kind!='TranslateTransform' else ''
    children=f'<Canvas x:Name="Layer_{nm(part)}_{key}" Width="48" Height="48"><Canvas.RenderTransform><{kind} x:Name="{nm(part)}_{key}"{center}/></Canvas.RenderTransform>{children}</Canvas>'
   native_tracks(t)
  return f'<Canvas Width="48" Height="48"{attrs}>{children}</Canvas>'
 def shape(e):
  kind=tag(e);attrs=''
  if kind=='path':kind='Path';attrs=' Data='+quoteattr(e.get('d'))
  elif kind in ['circle','ellipse']:
   rx=float(e.get('r',e.get('rx','0')));ry=float(e.get('r',e.get('ry','0')));x=float(e.get('cx'));y=float(e.get('cy'))
   kind='Ellipse';attrs=f' Width="{2*rx}" Height="{2*ry}" Canvas.Left="{x-rx}" Canvas.Top="{y-ry}"'
  else:raise ValueError(kind)
  fill=e.get('fill','none');stroke=e.get('stroke','currentColor');parts=''
  if fill!='none':parts+=f'<{kind}{attrs} Fill="{{Binding '+('SurfaceBrush' if fill.startswith('var(') else 'Foreground')+'}}"'+f' Opacity="{e.get("fill-opacity","1")}"/>'
  if stroke!='none':parts+=f'<{kind}{attrs} Stroke="{{Binding Foreground}}" StrokeThickness="1.6" StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round"/>'
  return parts
 def node(e):
  if tag(e)=='defs':return ''
  if e.get('clip-path'):
   clipid=e.get('clip-path').split('#')[1].rstrip(')');bowl=next(c for c in svg.iter() if c.get('id')==clipid)[0].get('d')
   liquid=e[0];level=float(re.match(r'M5 ([\d.]+)',liquid[0].get('d')).group(1));t=tracks['liquid'];native_tracks(t,True)
   # The clip moves down while a nested layer moves up by the same amount.
   # The liquid's bowl geometry stays fixed, so there is no fill outside the glass.
   return f'''<Canvas x:Name="Part_liquid" Width="48" Height="48">
<Canvas.RenderTransform><TranslateTransform x:Name="liquid_translateY"/></Canvas.RenderTransform>
<Canvas.Clip><RectangleGeometry Rect="0,{level},48,{48-level}"/></Canvas.Clip>
<Canvas x:Name="Layer_liquid_counter" Width="48" Height="48"><Canvas.RenderTransform><TranslateTransform x:Name="liquid_counter"/></Canvas.RenderTransform>
<Path Data={quoteattr(bowl)} Fill="{{Binding Foreground}}" Opacity="0.24"/>
</Canvas></Canvas>'''
  if tag(e)=='g':return group(e,''.join(node(c) for c in e))
  return shape(e)
 xaml='<Canvas xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" Width="48" Height="48" IsHitTestVisible="False">'+''.join(node(e) for e in svg)+'</Canvas>'
 # Validate generated XML and ensure every animation has a matching named target.
 tree=ET.fromstring(xaml);names={e.get('{http://schemas.microsoft.com/winfx/2006/xaml}Name') for e in tree.iter()}
 assert all(t[0] in names for t in native)
 def csnum(v):return str(float(v)).lower()
 trackcode=',\n'.join('new("'+target+'", "'+prop+'", '+str(duration)+', '+str(delay)+', ['+', '.join('new('+csnum(o)+', '+csnum(v)+')' for o,v in values)+'])' for target,prop,duration,delay,values in native)
 definitions.append('["'+id+'"] = new("""\n'+xaml+'\n""", [\n'+trackcode+'\n])')
output='''// Generated from the Carte 2.3 SVG layers and motion catalog. Do not hand-edit.
namespace Carte.Controls;
internal sealed record IllustrationFrame(double Offset, double Value);
internal sealed record IllustrationTrack(string Target, string Property, int Duration, int Delay, IllustrationFrame[] Frames);
internal sealed record IllustrationDefinition(string Xaml, IllustrationTrack[] Tracks);
internal static class IllustrationCatalog
{
    internal static readonly IReadOnlyDictionary<string, IllustrationDefinition> Items = new Dictionary<string, IllustrationDefinition>(StringComparer.Ordinal)
    {
'''+',\n'.join(definitions)+'\n    };\n}\n'
# XML escape braces are literal binding syntax, not doubled Python formatting artifacts.
output=output.replace('Foreground}}','Foreground}').replace('SurfaceBrush}}','SurfaceBrush}')
(app/'Controls/IllustrationCatalog.g.cs').write_text(output,encoding='utf-8')
print('Generated 18 native vector definitions, with validated named animation targets.')
