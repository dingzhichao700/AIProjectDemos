"""Explicit item-ID, independent image and authoritative display-map checks."""
import json,re

def audit_item_icons(project,policy,records):
 rule=policy.get('itemIconGovernance')
 if not rule:return []
 result=[]
 if not any((project/rule[key]).exists() for key in ['itemsFile','mapFile']) and not any(path.startswith(rule['root']+'/') for path in records):return []
 def add(code,path,message):result.append(dict(code=code,path=path,guid=records.get(path,{}).get('guid'),severity='error',message=message))
 try:
  items=json.loads((project/rule['itemsFile']).read_text(encoding='utf-8-sig'));data=json.loads((project/rule['mapFile']).read_text(encoding='utf-8-sig'));entries=data['entries'];by_id={r['itemId']:r for r in entries}
  if len(by_id)!=len(entries):raise ValueError('Duplicate item identity in display map')
  ids={row['id'] for row in items}
  for row in items:
   item=row['id'];expected=f"{rule['root']}/{item}.png"
   if row.get('iconId')!=item:add('ITEM_ICON_IDENTITY',rule['itemsFile'],'iconId must equal the stable item ID: '+str(item))
   if item not in by_id or by_id[item].get('content')!=expected:add('ITEM_ICON_ADDRESS',rule['mapFile'],'Expected '+expected)
   if expected not in records:add('ITEM_ICON_MISSING',rule['mapFile'],'Missing '+expected)
  for item in by_id:
   if item not in ids:add('ITEM_ICON_OWNER',rule['mapFile'],'Unknown item '+str(item))
  for path in records:
   if path.startswith(rule['root']+'/'):
    match=re.fullmatch(re.escape(rule['root'])+r'/(\d+)\.png',path)
    if not match or int(match[1]) not in ids:add('ITEM_ICON_LAYOUT',path,'Item icons must be flat ID.png files for a configured item')
 except (KeyError,ValueError,TypeError,OSError) as error:add('ITEM_ICON_SCHEMA',rule.get('mapFile','Assets'),str(error))
 return result
