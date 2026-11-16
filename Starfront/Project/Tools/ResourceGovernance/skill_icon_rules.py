"""Card/skill-owned icon checks; invoked by the sole resource governance audit."""
import json,re
from pathlib import Path

def audit_skill_icons(project,policy,records):
    rule=policy.get('skillIconGovernance')
    if not rule:
        return []
    flat=rule.get('loadingMode')=='unpack-flat'
    result=[]
    def add(code,path,message,severity='error',**extra):
        result.append(dict(code=code,path=path,guid=records.get(path,{}).get('guid'),severity=severity,message=message,**extra))
    skills_path=rule['skillsFile']; entries_path=rule['entriesFile']; cards_path=rule['cardsFile']
    roots=(rule['independentRoot']+'/',rule['atlasSourceRoot']+'/')
    present=[(project/p).exists() for p in [skills_path,entries_path,cards_path]]
    if not any(present) and not any(p.startswith(roots) for p in records):
        return []
    if not all(present):
        add('SKILL_ICON_TABLES',skills_path,'Missing skill, card-entry or card authority table')
        return result
    try:
        read=lambda p:json.loads((project/p).read_text(encoding='utf-8-sig'))
        skills=read(skills_path);entries=read(entries_path);cards={r['id'] for r in read(cards_path)}
        if not isinstance(skills,list) or not isinstance(entries,list):
            raise ValueError('Expected arrays')
        by_id={r['id']:r for r in skills}
        if len(by_id)!=len(skills):
            raise ValueError('Duplicate skill ID')
        owners={sid:set() for sid in by_id}
        for row in entries:
            if row['skillId'] not in by_id or row['cardId'] not in cards:
                add('SKILL_ICON_OWNER',entries_path,'Unknown card/skill relation: '+str(row))
            else:
                owners[row['skillId']].add(row['cardId'])
        legacy=rule.get('legacyIcons',[])
        ids=set()
        for item in legacy:
            key=(item['cardId'],item['skillId'])
            if key in ids or not all(item.get(k) for k in ['address','source','sourceGuid','owner','basis','reviewBatch','exitCondition']) or '*' in item['source']:
                raise ValueError('Invalid exact skill-icon legacy entry')
            ids.add(key)
        declared_shared=rule.get('sharedIcons',[])
        for sid,row in by_id.items():
            address=row.get('icon',''); card_owners=owners[sid]
            if not card_owners:
                add('SKILL_ICON_OWNER',skills_path,'Skill has no card owner: '+str(sid));continue
            if len(card_owners)!=1:
                add('SKILL_ICON_OWNER',entries_path,'Multiple owners need explicit resource ownership: '+str(sid));continue
            card=next(iter(card_owners))
            independent=f"{rule['independentRoot']}/{sid}.png" if flat else f"{rule['independentRoot']}/{card}/{sid}.png"
            atlas=None if flat else f"{rule['atlasAddressRoot']}/{card}-{sid}"
            if address in [independent,atlas]:
                continue
            shared=[x for x in declared_shared if x.get('address')==address and x.get('ownerCardId') in cards and x.get('ownerSkillId') in by_id and [card,sid] in x.get('consumers',[]) and x.get('basis')]
            if shared:
                owner=shared[0]
                valid=[f"{rule['independentRoot']}/{owner['ownerSkillId']}.png"] if flat else [f"{rule['independentRoot']}/{owner['ownerCardId']}/{owner['ownerSkillId']}.png",f"{rule['atlasAddressRoot']}/{owner['ownerCardId']}-{owner['ownerSkillId']}"]
                if address in valid and owner['ownerCardId'] in owners[owner['ownerSkillId']]:
                    continue
            prior=[x for x in legacy if x['cardId']==card and x['skillId']==sid and x['address']==address and records.get(x['source'],{}).get('guid')==x['sourceGuid']]
            if prior:
                x=prior[0];add('SKILL_ICON_IDENTITY',x['source'],f'Pending card={card}, skill={sid}: '+address,'legacy',exceptionId=f'SKILL-{card}-{sid}',reviewBatch=x['reviewBatch'])
            else:
                add('SKILL_ICON_IDENTITY',skills_path,f'card={card}, skill={sid}: expected {independent} or {atlas}; got {address}')
        for path in records:
            if not path.startswith(roots) or not path.endswith('.png'):
                continue
            if any(x['source']==path and x['sourceGuid']==records[path]['guid'] for x in legacy):
                continue
            if flat:
                match=re.fullmatch(re.escape(rule['independentRoot'])+r'/(\d+)\.png',path)
                if not match or len(owners.get(int(match[1]),set()))!=1:
                    add('SKILL_ICON_ASSET_IDENTITY',path,'All skill icons must be independent PNGs flat under skillIcons with skill ID filenames')
                continue
            if path.startswith(rule['independentRoot']+'/'):
                match=re.fullmatch(re.escape(rule['independentRoot'])+r'/(\d+)/(\d+)\.png',path)
            else:
                match=re.fullmatch(re.escape(rule['atlasSourceRoot'])+r'/(\d+)-(\d+)\.png',path)
            if not match or int(match[1]) not in owners.get(int(match[2]),set()):
                add('SKILL_ICON_ASSET_IDENTITY',path,'Icon filename/directory must match the actual card-skill relationship')
        for path in records:
            if path.startswith('Assets/Scripts/') and path.endswith('.cs'):
                text=(project/path).read_text(encoding='utf-8-sig',errors='replace')
                if 'skill-v11-' in text or 'cardSkillDetail/skillVisual-' in text:
                    add('SKILL_ICON_VIEW_REMAP',path,'Views must consume the configured icon address; no type-based legacy remapping')
    except (KeyError,TypeError,ValueError,OSError) as error:
        add('SKILL_ICON_SCHEMA',skills_path,str(error))
    if flat:
        for group in (project/'Assets/AddressableAssetsData/AssetGroups').glob('*.asset'):
            if re.search(r'^    m_Address: '+re.escape(rule['atlasAddressRoot']+'.png')+r'\s*$',group.read_text(encoding='utf-8-sig'),re.M):
                add('SKILL_ICON_ATLAS_RUNTIME',group.relative_to(project).as_posix(),'Retired skill atlas cannot be registered alongside the independent icons')
        for old in policy.get('retiredRuntimeAssets',[]):
            if old['path'] in records and records[old['path']]['guid']!=old['guid']:
                add('RETIRED_ASSET_IDENTITY',old['path'],'Retired path identity changed; do not silently skip a different asset')
    return result
