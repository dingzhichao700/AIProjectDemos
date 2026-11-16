"""Approved current-project scene/common/cardBattle ownership; no historical-version adapters."""
import re

REVISION='RESOURCE-PACK-STRATEGY/R2'
COMMON={'uiMain','uiTeam','uiCultivation','uiSkillDetail','uiItem','uiDebug','cardDisplay','cardDisplayAnimation','icons','uiCommon','renderCommon','audioCommon'}

def enabled(policy):return isinstance(policy,dict) and policy.get('revision')==REVISION

def owner(group,key,path='',policy=None):
 if not enabled(policy):return group,key
 if group=='map':
  if not re.fullmatch(r'map_\d+',key or ''):raise ValueError('Scene ownership needs a declared map identity')
  return 'scene','scene_'+key[4:]
 if group=='battleCommon':return 'scene','scene_common_'+(key or 'unresolved')
 if group=='uiBattle':
  if '/playerMatching/' in path:return 'common','ui_playerMatching'
  return 'scene','scene_common_'+(key or 'battle_ui')
 if group=='icons':
  if (key or '').startswith('skill_'):return 'common','icons_skill'
  if (key or '').startswith('item_'):return 'common','icons_item'
  raise ValueError('Icon category is not declared')
 if group in COMMON:return 'common',key
 return group,key

def role(group,key,policy=None,former_group=None):
 if not enabled(policy):return None
 declared=policy.get('bundleRoles',{}).get(group+'/'+str(key))
 if declared:return declared
 if group=='cardBattle':return 'card-exclusive'
 if group=='default' and key in ['ui_playerMatching','ui_formationBackground','ui_formationEvents']:return 'ui-page'
 if group=='scene':
  if (key or '').startswith('scene_common_'):return 'scene-shared'
  if re.fullmatch(r'scene_\d+',key or ''):return 'scene-exclusive'
  return 'unresolved'
 if group=='common':
  if (key or '').startswith('icons_'):return 'shared-ui'
  if (key or '').startswith('display_'):return 'display-animation'
  if (key or '').startswith(('face_','portrait_')):return 'display-static'
  if former_group in ['uiCommon','renderCommon','audioCommon']:return 'shared-ui'
  return 'ui-page'
 if group in ['opening','title']:return 'early'
 return 'technical'

def annotated(group,key,policy,path='',former_group=None):
 group,key=owner(group,key,path,policy)
 return dict(group=group,bundleKey=key,domainRole=role(group,key,policy,former_group))

def violation(source,target):
 sr,tr=source.get('domainRole'),target.get('domainRole');sk,tk=source.get('bundleKey'),target.get('bundleKey')
 if sr is None and tr is None:return None
 if not sr or not tr:return False
 if tr=='technical':return False
 if sr=='early':return tr not in ['early','shared-ui']
 if sr=='shared-ui':return tr not in ['shared-ui']
 if sr in ['ui-page','display-static','display-animation']:
  if tr in ['scene-exclusive','scene-shared','card-exclusive','unresolved']:return True
  if tr=='ui-page':return sr!='ui-page' or sk!=tk
  if sr=='display-animation' and tr=='display-animation':return str(sk).split('_')[1:2]!=str(tk).split('_')[1:2]
  return False
 if sr=='scene-shared':return tr not in ['scene-shared','shared-ui']
 if sr=='scene-exclusive':return tr not in ['scene-shared','shared-ui','scene-exclusive'] or (tr=='scene-exclusive' and sk!=tk)
 if sr=='card-exclusive':return tr not in ['shared-ui','scene-shared','card-exclusive'] or (tr=='card-exclusive' and sk!=tk)
 return False

def validate_policy(data):
 if data is None:return
 if not enabled(data) or data.get('approvalId')!=REVISION or not isinstance(data.get('bundleRoles'),dict):raise ValueError('Missing exact approved resource-domain contract')
 allowed={'shared-ui','ui-page','display-static','display-animation','scene-exclusive','scene-shared','card-exclusive','early','technical','unresolved'}
 if any('/' not in k or v not in allowed for k,v in data['bundleRoles'].items()):raise ValueError('Invalid explicit Bundle role')

def validate_nonconsumer_roots(manifest):
 policy=manifest.get('resourceDomainPolicy')
 if not enabled(policy):return
 rows={r['guid']:r for r in manifest['records']};overrides={o['guid']:o for o in manifest.get('packingOverrides',[])}
 for guid,evidence in policy.get('nonConsumerRoots',{}).items():
  row=rows.get(guid);override=overrides.get(guid)
  if not row or not override or override.get('purpose')!='retained-legacy' or not override['path'].startswith('Assets/Art/material/scene/battle/') or guid==manifest.get('stageContract',{}).get('mainRootGuid'):raise ValueError('Non-consumer root must be an exact previously isolated unused debug material/texture')
  if set(evidence)!={'path','assetSha256','evidence'} or any(evidence[k]!=override[k] for k in evidence) or not evidence['evidence']:raise ValueError('Non-consumer evidence differs from original actual-request proof')

def validate_override(row,override,policy):
 if not enabled(policy):raise ValueError('Domain override has no current-project approval')
 if row['state'] not in ['current','target','keep','hold'] or (row['state']=='hold' and not row['current'].get('entries')):raise ValueError('Cannot activate a held preview through regrouping')
 group,key=owner(row['target'].get('group'),row['target'].get('bundleKey'),override['path'],policy)
 expected={'group':group,'address':override['path'],'labels':[key]}
 if group not in ['common','scene'] or override['entry']!=expected:raise ValueError('Domain override differs from approved exact owner/key')
 previous=override.get('previousOverride')
 if previous and (previous.get('guid')!=row['guid'] or previous.get('path')!=override['path'] or previous.get('assetSha256')!=override['assetSha256']):raise ValueError('Superseded override identity drift')

def validate_regroup_selection(selected,approved,current):
 """Reject drift before any AA write; current is the freshly read GUID snapshot."""
 if not selected:raise ValueError('Empty regroup selection')
 authority={r['guid']:r for r in approved};seen=set()
 for item in selected:
  guid=item['guid']
  if guid in seen:raise ValueError('Duplicate regroup GUID')
  seen.add(guid)
  baseline=authority.get(guid);actual=current.get(guid)
  if not baseline or not actual:raise ValueError('Unapproved or missing regroup resource')
  for key in ['path','sha256','before','after']:
   if item.get(key)!=baseline.get(key):raise ValueError('Selection differs from approved mapping: '+key)
  if any(actual.get(k)!=item[k] for k in ['path','sha256','before']):raise ValueError('Current asset or AA registration changed')
  before,after=item['before'],item['after']
  if before['address']!=after['address'] or after['address']!=item['path']:raise ValueError('Regroup cannot change public addresses')
  if after['group'] not in ['common','scene'] or len(after['labels'])!=1 or not after['labels'][0]:raise ValueError('Missing exact domain Bundle identity')
 return len(seen)
