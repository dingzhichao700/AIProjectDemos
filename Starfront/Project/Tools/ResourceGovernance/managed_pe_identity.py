"""Parse CLR identity and narrowly compare a single CodeView symbol GUID."""
import struct,hashlib,uuid
def module_mvid(data):
 def u16(o):return struct.unpack_from('<H',data,o)[0]
 def u32(o):return struct.unpack_from('<I',data,o)[0]
 try:
  pe=u32(60)
  if data[:2]!=b'MZ' or data[pe:pe+4]!=b'PE\0\0':raise ValueError('Invalid PE')
  optional=pe+24;magic=u16(optional)
  if magic not in (0x10b,0x20b):raise ValueError('Unknown optional header')
  directory=optional+(96 if magic==0x10b else 112)
  sections=optional+u16(pe+20)
  def rva(value,size):
   matches=[]
   for i in range(u16(pe+6)):
    row=sections+40*i;virtual=u32(row+12);rawsize=u32(row+16);raw=u32(row+20)
    if virtual<=value and value+size<=virtual+rawsize and raw+value-virtual+size<=len(data):matches.append(raw+value-virtual)
   if len(matches)!=1:raise ValueError('Unmapped/ambiguous metadata RVA')
   return matches[0]
  cli=rva(u32(directory+14*8),72);mdsize=u32(cli+12);md=rva(u32(cli+8),mdsize)
  if data[md:md+4]!=b'BSJB':raise ValueError('Missing CLR metadata')
  position=md+16+u32(md+12);position=(position+3)&~3;count=u16(position+2);position+=4;streams={}
  for _ in range(count):
   offset,size=u32(position),u32(position+4);end=data.index(b'\0',position+8,min(position+40,len(data)));name=data[position+8:end].decode('ascii');position=(end+4)&~3
   if name in streams or offset+size>mdsize:raise ValueError('Invalid/duplicate metadata stream')
   streams[name]=(md+offset,size)
  guid,guidsize=streams['#GUID'];table,tablesize=streams.get('#~',streams.get('#-',(0,0)))
  if guidsize!=16 or not table or tablesize<24:raise ValueError('Only a unique standalone MVID GUID heap is supported')
  valid=struct.unpack_from('<Q',data,table+8)[0]
  if not valid&1:raise ValueError('Missing Module table')
  rows=table+24;modulecount=u32(rows);row=rows+4*valid.bit_count();flags=data[table+6];stringwidth=4 if flags&1 else 2;guidwidth=4 if flags&2 else 2
  read=u32 if guidwidth==4 else u16;mvidindex=read(row+2+stringwidth)
  if modulecount!=1 or u16(row)!=0 or mvidindex!=1 or read(row+2+stringwidth+guidwidth)!=0 or read(row+2+stringwidth+guidwidth*2)!=0:raise ValueError('Unsupported Module/MVID or Edit-and-Continue metadata')
  if row+2+stringwidth+guidwidth*3>table+tablesize or guid<position:raise ValueError('Metadata bounds')
  return dict(offset=guid,length=16,mvid=str(uuid.UUID(bytes_le=data[guid:guid+16])))
 except (struct.error,IndexError,KeyError,UnicodeError) as e:raise ValueError('Invalid CLR metadata') from e

def aot_codeview_equivalence(left,right):
 a,b=module_mvid(left),module_mvid(right)
 if len(left)!=len(right) or a!=b:raise ValueError('AOT layout or Module.Mvid differs')
 def codeview(data):
  def u32(o):return struct.unpack_from('<I',data,o)[0]
  def u16(o):return struct.unpack_from('<H',data,o)[0]
  pe=u32(60);optional=pe+24;directory=optional+(96 if u16(optional)==0x10b else 112);sections=optional+u16(pe+20)
  def rva(value,size):
   matches=[]
   for i in range(u16(pe+6)):
    row=sections+40*i;virtual=u32(row+12);rawsize=u32(row+16);raw=u32(row+20)
    if virtual<=value and value+size<=virtual+rawsize and raw+value-virtual+size<=len(data):matches.append(raw+value-virtual)
   if len(matches)!=1:raise ValueError('Invalid debug RVA')
   return matches[0]
  size=u32(directory+6*8+4)
  if not size or size%28 or size>28*64:raise ValueError('Invalid debug directory')
  table=rva(u32(directory+6*8),size);found=[]
  for row in range(table,table+size,28):
   if u32(row+12)!=2:continue
   length=u32(row+16);pointer=u32(row+24)
   if length<25 or rva(u32(row+20),length)!=pointer or data[pointer:pointer+4]!=b'RSDS' or data[pointer+length-1]!=0:raise ValueError('Invalid CodeView RSDS record')
   found.append(pointer+4)
  if len(found)!=1:raise ValueError('Require exactly one CodeView record')
  return found[0]
 try:offset=codeview(left)
 except (struct.error,IndexError) as e:raise ValueError('Invalid debug metadata') from e
 if codeview(right)!=offset:raise ValueError('CodeView layout differs')
 if left[:offset]!=right[:offset] or left[offset+16:]!=right[offset+16:]:raise ValueError('AOT contains changes outside CodeView symbol GUID')
 return dict(equivalent=True,onlyField='IMAGE_DEBUG_TYPE_CODEVIEW.RSDS.GUID',offset=offset,length=16,moduleMvid=a['mvid'],leftSymbolGuid=str(uuid.UUID(bytes_le=left[offset:offset+16])),rightSymbolGuid=str(uuid.UUID(bytes_le=right[offset:offset+16])),normalizedSha256=hashlib.sha256(left[:offset]+bytes(16)+left[offset+16:]).hexdigest())
