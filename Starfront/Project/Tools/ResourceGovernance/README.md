# Resource governance tooling v1.0.0

Python 3.10+ standard library. No pip packages, automatic resource moves, deletions or baseline acceptance. Exact project policy is `ProjectSettings/ResourceGovernance.json`.

From the Unity project directory:

```powershell
python Tools/ResourceGovernance/resource_governance.py --project . --output Library/ResourceGovernance/audit.json --journal-root <approved-directory-outside-project>
python Tools/ResourceGovernance/resource_governance.py --project . --scope Assets/Art/atlasSource/default/mainView --output Library/ResourceGovernance/pack.json --journal-root <approved-directory-outside-project>
python Tools/ResourceGovernance/resource_governance.py --project . --preflight-plan <exact-approved-move-plan.json>
```

Exit 0: no new blocking errors, but inspect named legacy/duplicate-review items. Exit 2: violations. Exit 3: invalid policy, input or environment; fail closed. An audit never proves unused assets may be deleted. Full-path sprite checks do not replace all dynamic-reference, Editor dependency, real UI or Bundle tests.

Editor menu: Tools/Resources/Check All Resources. Interpreter and durable journal directory are machine-local EditorPrefs (or RESOURCE_GOVERNANCE_PYTHON / RESOURCE_GOVERNANCE_JOURNAL_ROOT environment variables). No user absolute interpreter path belongs in the shared policy.

`ResourceGovernanceGate.ValidateOrThrow(stage, scopes)` is the shared Editor integration; `BatchValidate()` performs only validation, never a build/release. Import watching only reports after the editor becomes idle; pack/AA/content gates fail closed. Legacy entries require exact code/path/GUID and owner/basis/reviewBatch/exitCondition, not wildcards. Do not deploy a baseline-generation command as a production bypass.

`ResourceMoveTransaction.Execute(planPath,journalRoot,updateDependenciesAndVerify)` requires an owned Edit/idle slot. The callback must update and verify all declared dependent resources/configuration. Existing affected asset files and metas must be listed in `affectedFiles`; their bytes are snapshotted. Move-only tool v1 does not edit source tables automatically and rejects all deletion requests. Source-table mutation requires the owning batch to include explicit source-table backup and original Luban generation in its transaction design.

Journals and backups persist outside Library/project. Incomplete journals block later controlled operations. Only the active transaction's exact verifying journal is exempted in its own postcheck; other incomplete journals remain errors. Crashed journals are inspected before any manual recovery; no blind automatic replay. Successful/failed temporary self-test fixture journals remain evidence, not production migration approvals.

Tests: `python -m unittest -v test_resource_governance.py` from this tool directory. Test policy comes from the actual ProjectSettings file (or RG_POLICY_PATH in an isolated harness).

Card/skill icon extension (B1-01/R2): the sole CLI imports `skill_icon_rules.py`; both are hash-locked. Policy `skillIconGovernance` names the actual authority tables and the independent/flat atlas identity roots. Exact 60-icon legacy entries remain visible with owner/batch/exit conditions. Use `python -m unittest -v test_resource_governance test_skill_icon_rules`. No View type remapping or unregistered cross-skill copies.

B1-01/R3 supersedes prior icon layouts: all skills are flat independent skillIcons/{skillId}.png, all item contents are flat itemIcons/{itemId}.png. The old skill atlas is retained pending deletion approval but excluded by exact path/GUID from runtime registration. The CLI imports both hash-locked skill_icon_rules.py and item_icon_rules.py. Run unittest discovery for the original, skill, item and retired-atlas tests. Source/config/data-map/UI consumer verification remains mandatory; no physical deletion is implied.

## RESOURCE-PACK/R1 P0

ProjectSettings/ResourcePackManifest.json is the locked migration-state and bundle-routing contract. pack_rules.py is the only semantic classifier; ResourcePackRouting consumes its audit report. Existing current/hold/retired/keep entries are not reassigned by a scan. Exact packingOverrides are the explicit exception for approved retained-path performance repairs: GUID/path/content hash, original registration history, purpose and typed target owner/key are mandatory; only the declared identities can be assigned, original current/target history is preserved. This can share an already-consumed implicit legacy source without admitting arbitrary new atlas sources. Missing/stale override identity, extra labels, changed content or post-registration mismatch fail closed. New assets must use approved typed routes; no default/scene fallback. P0 user acceptance is recorded; each production transition now requires an approved named testScope and exact GUID subset.

Default audit, --scope Assets and --scope Assets/ are equivalent. Do not pass --allow-pending-registration in content/release gates: it exists solely for the assignment producer's pre-check and permits only a known transition's exact old registration or an unregistered new asset. Assignment post-check is strict.

ResourceMoveTransaction binds source/target/GUID to the approved batch, backs up Manifest/lock and affected group/schema settings, advances state in the durable transaction and restores it on failure. It supports packTransitionGuids for registration-only batches. Deletion remains disabled.

ResourcePackP0Review.Run is an Editor self-check using owned throwaway fixtures with finally restoration, not a migration/publish command. P0 establishes rules/tools only; per-batch runtime and actual build dependency evidence remain required. RESOURCE-PACK-NORM/R1 also freezes Stage/Login source contract and declared data-texture import properties in the same Manifest; drift blocks the gate.

RESOURCE-PACK/R2: card_suite_rules.py is invoked by the same canonical audit and locked with Manifest.cardSuites. It checks the visible-card usage matrix, actual two-source skill/action requirements, exact retained legacy identity, candidate/active/retained provenance and explicit flat-building owner routing. No source assets/configuration are changed by this check. Runtime preload correction and Editor/device validation remain later batches.

ART-BUSINESS-LAYOUT/R1: atlasBusinessLayout explicitly maps base/business-atlas directories to runtime owners. Source/output names correspond while AA Group is independent. No unknown-business fallback or source runtime registration; existing GUID-specific transitional records remain exact. test_atlas_business_rules.py covers positive and negative ownership/layout cases.
