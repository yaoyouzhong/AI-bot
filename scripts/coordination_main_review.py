"""Pure main-only observation and native read-only review metadata; no checkpoint DB."""
import hashlib
import re
from coordination_checkpoint import require, canonical, text


def sha(value):
    return isinstance(value, str) and re.fullmatch('[0-9a-f]{40}', value) is not None


def baseline(repository, remote_head, targets, evidence):
    """Record observation only. Target snapshots are evidence, never alignment receipts."""
    require(isinstance(repository,str) and re.fullmatch(r'[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+',repository), 'Repository required')
    require(sha(remote_head) and text(evidence), 'Verified main SHA and evidence required')
    require(isinstance(targets,dict) and set(targets)=={'windows','macos'}, 'Both target observations required')
    observations = {}
    for platform, entry in targets.items():
        require(isinstance(entry,dict) and sha(entry.get('head')) and text(entry.get('evidence')), 'Target HEAD evidence required')
        require(entry.get('relation') in ('equal','behind','ahead','diverged','unknown'), 'Explicit Git relation required')
        require((entry['head']==remote_head) == (entry['relation']=='equal'), 'Relation contradicts observed SHA')
        observations[platform] = dict(observedHead=entry['head'], relation=entry['relation'],
                                      evidence=entry['evidence'], reviewedHead=None, alignedHead=None,
                                      pendingReview=dict(base=entry['head'],head=remote_head),
                                      status='awaiting-target-review')
    return dict(schemaVersion=1,repository=repository,branch='main',observedHead=remote_head,
                evidence=evidence,targets=observations,developmentCompletionAsserted=False,publicationPerformed=False)


def review_event(repository, branch, head, binding):
    require(binding.get('mode') == 'review-only', 'Read-only entry requires review mode')
    return _event(repository, branch, head, binding)


def alignment_event(repository, branch, head, binding):
    require(binding.get('mode') == 'isolated-alignment' and binding.get('isolatedWorkspaceVerified') is True
            and text(binding.get('workspaceRef')), 'Verified isolated worktree required')
    require(binding.get('autoPush') is False and binding.get('autoMerge') is False
            and binding.get('autoRelease') is False, 'No automatic external code publication')
    return _event(repository, branch, head, binding)


def _event(repository, branch, head, binding):
    """Route fixed main to a native task-specific read-only review; don't infer origin."""
    require(branch=='main' and sha(head), 'Only exact main and full SHA accepted')
    require(isinstance(binding,dict) and binding.get('verified') is True and text(binding.get('evidence')),
            'Trusted local review binding required')
    require(repository==binding.get('repository'), 'Repository outside approved binding')
    mode=binding.get('mode')
    require((mode=='review-only' and binding.get('access')=='read') or
            (mode=='isolated-alignment' and binding.get('access')=='workspace-write'), 'Unsupported execution scope')
    require(binding.get('targetPlatform') in ('windows','macos'), 'Fixed target required')
    require(isinstance(binding.get('eventType'),str) and re.fullmatch('[A-Za-z0-9_.:-]{1,64}',binding['eventType']), 'Fixed event type required')
    identity=hashlib.sha256(canonical([repository.casefold(),'main',head,binding['targetPlatform'],mode,binding.get('workspaceRef') if mode=='isolated-alignment' else None,'v1']).encode()).hexdigest()
    detail=canonical(dict(schema=1,repository=repository,branch='main',sourceCommit=head,
                          targetPlatform=binding['targetPlatform'],mode=mode,sourcePlatform=None,workspaceRef=binding.get('workspaceRef') if mode=='isolated-alignment' else None,
                          autoPush=False,autoMerge=False,autoRelease=False,
                          instructionsMeaning='Metadata only; trusted native agent scope controls actions. Analyze main changes; if authorized in isolated workspace align when needed, otherwise report no change. No automatic push, merge or release; no alignment claim without evidence'))
    return dict(id='main-task:'+identity,type=binding['eventType'],subject='main-task:'+identity,
                summary='Main snapshot requires target analysis; execution scope fixed locally; origin not inferred',detail=detail,
                link='https://github.com/'+repository+'/commit/'+head)
