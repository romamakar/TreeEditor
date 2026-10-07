document.addEventListener('DOMContentLoaded', function(){
    const dbRoot = document.getElementById('db-root');
    const cachedRoot = document.getElementById('cached-root');
    const refreshBtn = document.getElementById('refresh-cache');
    const applyBtn = document.getElementById('apply-cache');
    const resetBtn = document.getElementById('reset-db');

    // apiFetch: shared wrapper that attaches the persistent client cache id.
    async function apiFetch(path, opts){
        return fetch(buildUrl(path), mergeOpts(opts));
    }

    async function apiJson(path, opts){
        const r = await apiFetch(path, opts);
        if (!r.ok) throw new Error('HTTP ' + r.status);
        return r.json().catch(()=>null);
    }

    function mergeOpts(opts){
        opts = opts || {};
        opts.headers = opts.headers || {};
        try {
            // ensure window.clientId is populated from localStorage if available
            try {
                if (!window.clientId && window.localStorage) {
                    var cid = localStorage.getItem('tree-editor-client-id');
                    if (cid) {
                        window.clientId = cid;
                        console.debug('mergeOpts: recovered clientId from localStorage', cid);
                    }
                }
            } catch(e) {
                console.debug('mergeOpts: localStorage access failed', e);
            }

            if (window.clientId) {
                opts.headers['X-Client-Id'] = window.clientId;
                console.debug('mergeOpts: set X-Client-Id=', window.clientId);
            } else {
                console.debug('mergeOpts: no window.clientId');
            }
        } catch(e) {}
        return opts;
    }

    function buildUrl(path){
        const base = (window.apiBase || '').replace(/\/$/, '');
        if (!path) return base;
        if (path.startsWith('http://') || path.startsWith('https://')) return path;
        return base + path;
    }

    // DB tree
    const expandedIds = new Set();

    async function loadChildren(parentId){
        const qs = parentId==null ? '' : '?parentId=' + encodeURIComponent(parentId);
        const nodes = await apiJson('/api/children' + qs, { method: 'GET' });
        return nodes || [];
    }

    function renderDbNode(node){
        const div = document.createElement('div');
        const label = document.createElement('span');
        label.className = 'clickable';
        label.dataset.nodeId = node.id;
        label.textContent = node.value;
        const loadBtn = document.createElement('button');
        loadBtn.type = 'button';
        loadBtn.textContent = 'Load';
        loadBtn.className = 'btn btn-sm btn-outline-primary';
        loadBtn.addEventListener('click', async ()=>{
            loadBtn.disabled = true;
            try {
                await apiJson(`/api/cache/load/${node.id}`, { method: 'POST' });
                // after load, refresh cached view
                await refreshCached();
            } catch (e) {
                console.error('Load to cache failed', e);
                alert('Failed to load element to cache: ' + (e && e.message));
            } finally {
                loadBtn.disabled = false;
            }
        });

        div.appendChild(label);
        div.appendChild(loadBtn);

        // expand toggle
        let expanded = false;
        let childContainer = div.querySelector('ul');
        label.addEventListener('click', async ()=>{
            // refresh reference to child container in case it was created externally
            childContainer = div.querySelector('ul');
            if (!expanded){
                // first click: expand and load children lazily
                expanded = true;
                if (!childContainer){
                    childContainer = document.createElement('ul');
                    childContainer.style.listStyle = 'none';
                    childContainer.style.paddingLeft = '18px';
                    div.appendChild(childContainer);
                }
                // always (re)load children when expanding so applied DB changes appear
                childContainer.innerHTML = '';
                try {
                    const children = await loadChildren(node.id);
                    children.forEach(c => {
                        const li = document.createElement('li');
                        li.appendChild(renderDbNode(c));
                        childContainer.appendChild(li);
                    });
                } catch (e) {
                    console.error('Failed to load children', e);
                    childContainer.appendChild(document.createTextNode('Failed to load'));
                }
                childContainer.style.display = 'block';
                expandedIds.add(node.id);
            } else {
                // when already expanded, refresh children from DB (show applied changes)
                if (!childContainer) return;
                childContainer.innerHTML = '';
                try {
                    const children = await loadChildren(node.id);
                    children.forEach(c => {
                        const li = document.createElement('li');
                        li.appendChild(renderDbNode(c));
                        childContainer.appendChild(li);
                    });
                } catch (e) {
                    console.error('Failed to refresh children', e);
                    childContainer.appendChild(document.createTextNode('Failed to load'));
                }
            }
        });

        return div;
    }

    async function renderDbRoot(){
        dbRoot.innerHTML = '';
        dbRoot.appendChild(document.createTextNode('Loading...'));
        const nodes = await loadChildren(null);
        dbRoot.innerHTML = '';
        if (!nodes.length){
            dbRoot.appendChild(document.createTextNode('No nodes'));
            return;
        }
        const ul = document.createElement('ul');
        ul.style.listStyle = 'none';
        ul.style.paddingLeft = '0';
        nodes.forEach(n => {
            const li = document.createElement('li');
            li.appendChild(renderDbNode(n));
            ul.appendChild(li);
        });
        dbRoot.appendChild(ul);
    }

    // Ensure a node with given id is expanded and its children loaded
    async function ensureExpand(nodeId){
        try{
            const label = document.querySelector(`span[data-node-id="${nodeId}"]`);
            if (!label) return;
            const div = label.parentElement;
            if (!div) return;
            let childContainer = div.querySelector('ul');
            if (!childContainer){
                childContainer = document.createElement('ul');
                childContainer.style.listStyle = 'none';
                childContainer.style.paddingLeft = '18px';
                div.appendChild(childContainer);
            }
            childContainer.innerHTML = '';
            const children = await loadChildren(nodeId);
            children.forEach(c => {
                const li = document.createElement('li');
                li.appendChild(renderDbNode(c));
                childContainer.appendChild(li);
            });
            childContainer.style.display = 'block';
        } catch(e){ console.error('ensureExpand failed', e); }
    }

    // Cached tree
    async function refreshCached(){
        cachedRoot.innerHTML = '';
        cachedRoot.appendChild(document.createTextNode('Loading...'));
        const list = await apiJson('/api/cache');
        const roots = buildTree(list || []);
        cachedRoot.innerHTML = '';
        if (!roots.length){
            cachedRoot.appendChild(document.createTextNode('No cached elements'));
            return;
        }
        const ul = document.createElement('ul');
        roots.forEach(r => {
            const li = document.createElement('li');
            li.appendChild(renderCachedNode(r));
            ul.appendChild(li);
        });
        cachedRoot.appendChild(ul);
    }

    function buildTree(list){
        const map = new Map();
        list.forEach(i => map.set(i.id, { ...i, children: [] }));
        const roots = [];
        map.forEach(v => {
            if (v.parentId == null) roots.push(v);
            else if (map.has(v.parentId)) map.get(v.parentId).children.push(v);
            else roots.push(v);
        });
        return roots;
    }

    function renderCachedNode(node){
        const div = document.createElement('div');
        const span = document.createElement('span');
        span.textContent = (node.isDeleted ? '(deleted) ' : '') + node.value;
        if (node.isDeleted) {
            span.className = 'text-muted';
        } else {
            span.className = 'clickable';
            span.dataset.nodeId = node.id;
        }
        div.appendChild(span);

        // Buttons: only show when node is not deleted
        if (!node.isDeleted) {
            const editBtn = document.createElement('button');
            editBtn.type = 'button';
            editBtn.className = 'btn btn-sm btn-secondary';
            editBtn.textContent = 'Edit';
            editBtn.addEventListener('click', async ()=>{
                const val = prompt('Edit value for ' + node.value, node.value);
                if (val == null) return;
                const res = await apiFetch(`/api/cache/${node.id}`, mergeOpts({ method: 'PUT', headers: {'Content-Type':'application/json'}, body: JSON.stringify({ value: val }) }));
                if (!res.ok) { alert('Edit failed'); return; }
                await refreshCached();
            });

            const addBtn = document.createElement('button');
            addBtn.type = 'button';
            addBtn.className = 'btn btn-sm btn-outline-success';
            addBtn.textContent = 'Add Child';
            addBtn.addEventListener('click', async ()=>{
                const val = prompt('Value for new child', 'New child');
                if (val == null) return;
                const res = await apiFetch(`/api/cache/${node.id}/add`, mergeOpts({ method: 'POST', headers:{'Content-Type':'application/json'}, body: JSON.stringify({ value: val }) }));
                if (!res.ok) { alert('Failed to add child (parent may be deleted)'); return; }
                const result = await res.json().catch(()=>null);
                if (!result) { alert('Add child failed'); return; }
                await refreshCached();
            });

            const delBtn = document.createElement('button');
            delBtn.type = 'button';
            delBtn.className = 'btn btn-sm btn-outline-danger';
            delBtn.textContent = 'Delete';
            delBtn.addEventListener('click', async ()=>{
                if (!confirm('Delete this element and all descendants?')) return;
                const res = await apiFetch(`/api/cache/${node.id}`, mergeOpts({ method: 'DELETE' }));
                if (!res.ok) { alert('Delete failed'); return; }
                await refreshCached();
            });

            div.appendChild(editBtn);
            div.appendChild(addBtn);
            div.appendChild(delBtn);
        }

        if (node.children && node.children.length){
            const ul = document.createElement('ul');
            node.children.forEach(c => {
                const li = document.createElement('li');
                li.appendChild(renderCachedNode(c));
                ul.appendChild(li);
            });
            div.appendChild(ul);
        }
        return div;
    }

    // wire buttons
    refreshBtn.addEventListener('click', refreshCached);
    applyBtn.addEventListener('click', async ()=>{
        await apiFetch('/api/cache/apply', mergeOpts({ method: 'POST' }));
        await refreshCached();
        // re-render DB tree and restore expanded nodes so applied changes are visible
        await renderDbRoot();
        const ids = Array.from(expandedIds);
        for (const id of ids) {
            await ensureExpand(id);
        }
    });
    resetBtn.addEventListener('click', async ()=>{ if (!confirm('Reset DB and clear cache?')) return; await fetch(buildUrl('/api/reset'), mergeOpts({ method: 'POST' })); await refreshCached(); await renderDbRoot(); });

    // initial load
    renderDbRoot();
    refreshCached();
});