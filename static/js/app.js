    const fmtBytes = bytes => {
        if (bytes === 0) return '0 B';
        const k = 1024, sizes = ['B', 'KB', 'MB', 'GB', 'TB'];
        const i = Math.floor(Math.log(bytes) / Math.log(k));
        return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
    };

    let globalWastedBytes = 0;
    const updateWastedStat = (addBytes) => {
        globalWastedBytes += addBytes;
        document.getElementById('stat-wasted').textContent = fmtBytes(globalWastedBytes);
    };

    // --- API CALLER ---
    const fetchJson = async (url, options) => {
        const r = await fetch(url, options);
        if (!r.ok) {
            const err = await r.json().catch(()=>({}));
            throw new Error(err.detail || "Server error");
        }
        return await r.json();
    };

    // --- ⚡ ROOT MODE DETECTOR & UI LOCKDOWN ---
    window.addEventListener('DOMContentLoaded', async () => {
        try {
            const status = await fetchJson('http://localhost:8000/api/system_status');
            
            // Linux visual updates & restrictions
            if (status.os === 'Linux') {
                document.getElementById('targetPath').value = '/home/';
                
                // Disable Windows-only features visually
                const windowsWarningHtml = `
                    <div style="background: #f7e8eb; color: var(--danger); padding: 16px; border-radius: 12px; margin-bottom: 20px; text-align: center; border: 1px solid rgba(143,52,67,0.2);">
                        <strong><i class="material-icons" style="vertical-align: middle;">block</i> OS Compatibility Lock</strong><br>
                        This feature relies on the Windows Registry and AppxPackages. It cannot be run on Linux.
                    </div>`;

                // Inject warnings into the Windows tabs
                document.getElementById('tab-windows').insertAdjacentHTML('afterbegin', windowsWarningHtml);
                document.getElementById('tab-debloat').insertAdjacentHTML('afterbegin', windowsWarningHtml);

                // Physically disable the action buttons in those tabs
                document.querySelectorAll('#tab-windows button, #tab-debloat button').forEach(btn => {
                    btn.disabled = true;
                    btn.style.opacity = '0.4';
                    btn.style.cursor = 'not-allowed';
                });
            }

            // Engage Lockdown Visuals for Root Mode
            if (status.is_root) {
                document.getElementById('rootBanner').style.display = 'block';
                
                // Find all deletion/mutating buttons and disable them
                document.querySelectorAll('.danger, #telemetryBtn').forEach(btn => {
                    btn.disabled = true;
                    btn.title = "Action permanently disabled in Root View mode";
                });
            }
        } catch (e) {
            console.log("Could not fetch system status. Assuming normal mode.", e);
        }
    });

    // --- TAB LOGIC ---
    document.querySelectorAll('.tab-btn').forEach(btn => {
        btn.addEventListener('click', () => {
            document.querySelectorAll('.tab-btn').forEach(b => b.classList.remove('active'));
            document.querySelectorAll('.tab-content').forEach(c => c.classList.remove('active'));
            btn.classList.add('active');
            document.getElementById(btn.dataset.target).classList.add('active');
        });
    });

    // --- 1. VISUAL MAPPER ---
    document.getElementById('runMapperBtn').addEventListener('click', async () => {
        const path = document.getElementById('targetPath').value;
        const ignore = document.getElementById('ignoreFolders').value;
        const loader = document.getElementById('mapperLoader');
        const chartDiv = document.getElementById('chart');

        Plotly.purge(chartDiv);
        loader.style.display = 'block';

        try {
            const url = `http://localhost:8000/api/scan?target_path=${encodeURIComponent(path)}&ignore_folders=${encodeURIComponent(ignore)}`;
            const data = await fetchJson(url);
            const trace = [{
                type: "sunburst", ids: data.ids, labels: data.labels,
                parents: data.parents, values: data.values,
                branchvalues: 'total', maxdepth: 3,
                textinfo: "label+value+percent parent",
                hovertemplate: '<b>%{label}</b><br>Size: %{value} bytes<br><extra></extra>',
                marker: { line: { width: 1, color: '#f6efe4' } }
            }];
            Plotly.newPlot(chartDiv, trace, { margin: { l: 0, r: 0, b: 0, t: 0 }, paper_bgcolor: 'transparent', sunburstcolorway: ["#24543f","#b17d3f","#3b6695","#8f4f3b","#607d5a"] });
        } catch (error) { alert("Mapper Error: " + error.message); } 
        finally { loader.style.display = 'none'; }
    });

    // --- 2. DEDUPLICATOR ---
    document.getElementById('runDedupBtn').addEventListener('click', async () => {
        const path = document.getElementById('targetPath').value;
        const ignore = document.getElementById('ignoreFolders').value;
        const loader = document.getElementById('dedupLoader');
        const resultsDiv = document.getElementById('dedupResults');
        const deleteRow = document.getElementById('deleteActionRow');

        resultsDiv.innerHTML = '';
        deleteRow.style.display = 'none';
        loader.style.display = 'block';

        try {
            const url = `http://localhost:8000/api/duplicates?target_path=${encodeURIComponent(path)}&ignore_folders=${encodeURIComponent(ignore)}`;
            const duplicates = await fetchJson(url);
            
            if (duplicates.length === 0) {
                resultsDiv.innerHTML = '<p class="muted" style="text-align: center; padding: 40px;">Drive is clean! No duplicates found.</p>';
                return;
            }

            let grandTotalWasted = 0;
            let finalHtmlString = ""; 

            duplicates.forEach(group => {
                const groupWasted = group.size_bytes * (group.files.length - 1);
                grandTotalWasted += groupWasted;
                
                let groupHtml = `
                <div class="card duplicate-group" style="padding: 16px; margin-bottom: 16px;">
                    <div style="display: flex; justify-content: space-between; margin-bottom: 12px;">
                        <strong>Identical Files Found</strong>
                        <span class="pill danger">Wasting ${fmtBytes(groupWasted)}</span>
                    </div>
                    <p class="muted" style="margin-top: 0; font-size: 0.85em;">File Size: ${fmtBytes(group.size_bytes)} | SHA-256: ${group.hash.substring(0, 16)}...</p>
                    <div style="background: rgba(0,0,0,0.02); border-radius: 8px; padding: 0 12px;">`;
                
                group.files.forEach((filePath, idx) => {
                    const isChecked = idx > 0 ? 'checked' : '';
                    const tag = idx === 0 ? '<span class="pill" style="font-size: 0.7em; padding: 2px 8px;">Original</span>' : '';
                    groupHtml += `
                        <div class="list-row">
                            <input type="checkbox" class="dup-cb" value="${filePath}" ${isChecked}>
                            <span class="path-text">${filePath}</span> ${tag}
                        </div>`;
                });
                groupHtml += `</div></div>`;
                
                finalHtmlString += groupHtml; 
            });

            resultsDiv.innerHTML = finalHtmlString;

            if (grandTotalWasted > 0) {
                updateWastedStat(grandTotalWasted);
                deleteRow.style.display = 'block';
            }
        } catch (error) { alert("Deduplicator Error: " + error.message); } 
        finally { loader.style.display = 'none'; }
    });

    document.getElementById('executeDeleteBtn').addEventListener('click', async () => {
        const files = Array.from(document.querySelectorAll('.dup-cb:checked')).map(cb => cb.value);
        if (files.length === 0) return;
        if (!confirm(`Permanently delete ${files.length} files?`)) return;

        try {
            const res = await fetchJson('http://localhost:8000/api/delete', {
                method: 'POST', headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ file_paths: files })
            });
            alert(`Success! Deleted ${res.deleted.length} files.`);
            document.getElementById('runDedupBtn').click(); 
        } catch (error) { alert("Deletion Error: " + error.message); }
    });

    // --- 3. WINDOWS DEEP CLEAN ---
    document.getElementById('scanTempBtn').addEventListener('click', async () => {
        document.getElementById('scanTempBtn').textContent = 'Scanning...';
        try {
            const data = await fetchJson('http://localhost:8000/api/temp/scan');
            document.getElementById('tempSizeText').textContent = fmtBytes(data.total_bytes);
            document.getElementById('tempCountText').textContent = data.total_files;
            
            // Only enable the flush button if we are NOT locked down in root mode
            if (data.total_bytes > 0 && document.getElementById('rootBanner').style.display !== 'block') {
                document.getElementById('flushTempBtn').disabled = false;
            }
        } catch (error) { alert("Temp Scan Error: " + error.message); }
        finally { document.getElementById('scanTempBtn').textContent = 'Recalculate'; }
    });

    document.getElementById('flushTempBtn').addEventListener('click', async () => {
        if (!confirm("Aggressively delete Temp cache?")) return;
        try {
            const data = await fetchJson('http://localhost:8000/api/temp/flush', { method: 'POST' });
            alert(`Nuked ${data.deleted_count} items. Freed ${fmtBytes(data.freed_bytes)}. (Skipped ${data.locked_count} locked OS files).`);
            updateWastedStat(data.freed_bytes);
            document.getElementById('scanTempBtn').click(); 
        } catch (error) { alert("Flush Error: " + error.message); }
    });

    const ghostResultsDiv = document.getElementById('ghostResults');
    const deleteGhostsRow = document.getElementById('deleteGhostsRow');

    document.getElementById('runGhostsBtn').addEventListener('click', async () => {
        const loader = document.getElementById('ghostLoader');
        
        ghostResultsDiv.innerHTML = '';
        deleteGhostsRow.style.display = 'none';
        loader.style.display = 'block';

        try {
            const ghosts = await fetchJson('http://localhost:8000/api/ghosts');
            if (ghosts.length === 0) {
                ghostResultsDiv.innerHTML = '<p class="muted" style="text-align: center; padding: 20px;">No ghosts found. Registry matches AppData.</p>';
                return;
            }

            let htmlString = "";
            ghosts.forEach(ghost => {
                htmlString += `
                <div class="card ghost-group" style="padding: 16px; margin-bottom: 12px;">
                    <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 8px;">
                        <label style="display: flex; align-items: center; gap: 10px; cursor: pointer;">
                            <input type="checkbox" class="ghost-cb" value="${ghost.path}">
                            <strong style="font-size: 1.1em; color: var(--ink);">${ghost.name}</strong>
                        </label>
                        <span class="pill danger">${fmtBytes(ghost.size_bytes)}</span>
                    </div>
                    <p class="path-text" style="margin: 0; padding-left: 28px;">${ghost.path}</p>
                </div>`;
            });
            
            ghostResultsDiv.innerHTML = htmlString;
            deleteGhostsRow.style.display = 'block';

        } catch (error) { alert("Ghost Hunter Error: " + error.message); } 
        finally { loader.style.display = 'none'; }
    });

    document.getElementById('executeGhostDeleteBtn').addEventListener('click', async () => {
        const folders = Array.from(document.querySelectorAll('.ghost-cb:checked')).map(cb => cb.value);
        if (folders.length === 0) return;
        
        if (!confirm(`WARNING: You are about to permanently delete ${folders.length} AppData folder(s).\n\nAre you absolutely sure?`)) return;

        try {
            const res = await fetchJson('http://localhost:8000/api/ghosts/delete', {
                method: 'POST', headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ folder_paths: folders })
            });
            
            alert(`Success! Deleted ${res.deleted.length} orphaned folders.\nFreed ${fmtBytes(res.freed_bytes)} of space.`);
            updateWastedStat(res.freed_bytes);
            document.getElementById('runGhostsBtn').click(); 
        } catch (error) { alert("Ghost Deletion Error: " + error.message); }
    });

    // --- 4. OS DEBLOATER & PRIVACY ---
    document.getElementById('telemetryBtn').addEventListener('click', async () => {
        try {
            const res = await fetchJson('http://localhost:8000/api/debloat/telemetry', { method: 'POST' });
            document.getElementById('telemetryStatus').innerHTML = `<span style="color: green;">✔ ${res.message}</span>`;
            alert("Privacy Tweaks Applied Successfully!");
        } catch (error) { 
            document.getElementById('telemetryStatus').innerHTML = `<span style="color: red;">✖ ${error.message}</span>`;
            alert("Failed: You must restart your terminal/IDE as Administrator to apply registry tweaks."); 
        }
    });

    const bloatResultsDiv = document.getElementById('bloatResults');
    const deleteBloatRow = document.getElementById('deleteBloatRow');

    document.getElementById('scanBloatBtn').addEventListener('click', async () => {
        const loader = document.getElementById('bloatLoader');
        bloatResultsDiv.innerHTML = '';
        deleteBloatRow.style.display = 'none';
        loader.style.display = 'block';

        try {
            const apps = await fetchJson('http://localhost:8000/api/debloat/scan');
            if (apps.length === 0) {
                bloatResultsDiv.innerHTML = '<p class="muted" style="text-align: center;">No known bloatware found!</p>';
                return;
            }

            let htmlString = "";
            apps.forEach(app => {
                htmlString += `
                <div class="list-row" style="background: rgba(0,0,0,0.02); padding: 12px; border-radius: 8px; margin-bottom: 8px;">
                    <label style="display: flex; align-items: center; gap: 10px; cursor: pointer; width: 100%;">
                        <input type="checkbox" class="bloat-cb" value="${app.package_full_name}">
                        <div style="display: flex; flex-direction: column;">
                            <strong style="color: var(--ink);">${app.display_name}</strong>
                            <span class="path-text" style="font-size: 0.75em;">${app.id}</span>
                        </div>
                    </label>
                </div>`;
            });
            
            bloatResultsDiv.innerHTML = htmlString;
            deleteBloatRow.style.display = 'block';
        } catch (error) { alert("Scanner Error: " + error.message); } 
        finally { loader.style.display = 'none'; }
    });

    document.getElementById('executeBloatBtn').addEventListener('click', async () => {
        const packages = Array.from(document.querySelectorAll('.bloat-cb:checked')).map(cb => cb.value);
        if (packages.length === 0) return;
        
        if (!confirm(`Are you sure you want to run PowerShell to uninstall ${packages.length} system applications?`)) return;

        document.getElementById('executeBloatBtn').textContent = "Purging...";
        try {
            const res = await fetchJson('http://localhost:8000/api/debloat/remove', {
                method: 'POST', headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ packages: packages })
            });
            
            alert(`Success! Uninstalled ${res.success.length} bloatware packages.`);
            document.getElementById('scanBloatBtn').click(); 
        } catch (error) { alert("Uninstall Error: " + error.message); }
        finally { document.getElementById('executeBloatBtn').innerHTML = `<i class="material-icons">delete_sweep</i> Purge Selected Bloat`; }
    });

    // ---  5. FILE RADAR (Top 50 & Stale) ---
    const hogsResultsDiv = document.getElementById('hogsResults');
    const deleteHogsRow = document.getElementById('deleteHogsRow');

    document.getElementById('scanHogsBtn').addEventListener('click', async () => {
        const path = document.getElementById('targetPath').value;
        const ignore = document.getElementById('ignoreFolders').value;
        const loader = document.getElementById('hogsLoader');
        hogsResultsDiv.innerHTML = '';
        deleteHogsRow.style.display = 'none';
        loader.style.display = 'block';

        try {
            const url = `http://localhost:8000/api/radar/top50?target_path=${encodeURIComponent(path)}&ignore_folders=${encodeURIComponent(ignore)}`;
            const files = await fetchJson(url);
            if (files.length === 0) {
                hogsResultsDiv.innerHTML = '<p class="muted" style="text-align: center;">No large files found.</p>';
                return;
            }

            let htmlString = "";
            files.forEach(f => {
                htmlString += `
                <div class="list-row" style="background: rgba(0,0,0,0.02); padding: 12px; border-radius: 8px; margin-bottom: 8px;">
                    <label style="display: flex; align-items: center; gap: 10px; cursor: pointer; width: 100%;">
                        <input type="checkbox" class="radar-cb" value="${f.path}">
                        <div style="display: flex; flex-direction: column; width: 100%;">
                            <div style="display: flex; justify-content: space-between;">
                                <strong style="color: var(--ink); word-break: break-all; padding-right: 10px;">${f.name}</strong>
                                <span class="pill danger" style="padding: 2px 8px; white-space: nowrap; height: fit-content;">${fmtBytes(f.size_bytes)}</span>
                            </div>
                            <span class="path-text" style="font-size: 0.75em;">${f.path}</span>
                        </div>
                    </label>
                </div>`;
            });
            hogsResultsDiv.innerHTML = htmlString;
            deleteHogsRow.style.display = 'block';
        } catch (error) { alert("Error: " + error.message); } 
        finally { loader.style.display = 'none'; }
    });

    const staleResultsDiv = document.getElementById('staleResults');
    const deleteStaleRow = document.getElementById('deleteStaleRow');

    document.getElementById('scanStaleBtn').addEventListener('click', async () => {
        const path = document.getElementById('targetPath').value;
        const ignore = document.getElementById('ignoreFolders').value;
        const loader = document.getElementById('staleLoader');
        staleResultsDiv.innerHTML = '';
        deleteStaleRow.style.display = 'none';
        loader.style.display = 'block';

        try {
            const url = `http://localhost:8000/api/radar/stale?target_path=${encodeURIComponent(path)}&ignore_folders=${encodeURIComponent(ignore)}`;
            const files = await fetchJson(url);
            if (files.length === 0) {
                staleResultsDiv.innerHTML = '<p class="muted" style="text-align: center;">No old stale files found!</p>';
                return;
            }

            let htmlString = "";
            files.forEach(f => {
                const date = new Date(f.last_active_timestamp * 1000).toLocaleDateString();
                htmlString += `
                <div class="list-row" style="background: rgba(0,0,0,0.02); padding: 12px; border-radius: 8px; margin-bottom: 8px; border-left: 4px solid #1976d2;">
                    <label style="display: flex; align-items: center; gap: 10px; cursor: pointer; width: 100%;">
                        <input type="checkbox" class="radar-cb" value="${f.path}">
                        <div style="display: flex; flex-direction: column; width: 100%;">
                            <div style="display: flex; justify-content: space-between;">
                                <strong style="color: var(--ink); word-break: break-all; padding-right: 10px;">${f.name}</strong>
                                <span class="pill danger" style="padding: 2px 8px; white-space: nowrap; height: fit-content;">${fmtBytes(f.size_bytes)}</span>
                            </div>
                            <span class="path-text" style="font-size: 0.75em;">Last Modified: ${date}</span>
                        </div>
                    </label>
                </div>`;
            });
            staleResultsDiv.innerHTML = htmlString;
            deleteStaleRow.style.display = 'block';
        } catch (error) { alert("Error: " + error.message); } 
        finally { loader.style.display = 'none'; }
    });

    // Universal Radar File Deleter (Reuses Deduplicator Endpoint)
    const handleRadarDelete = async (btnId, clickEventBtn) => {
        const section = document.getElementById(btnId).closest('.panel');
        const files = Array.from(section.querySelectorAll('.radar-cb:checked')).map(cb => cb.value);
        
        if (files.length === 0) return;
        if (!confirm(`Permanently delete ${files.length} files?`)) return;

        try {
            const res = await fetchJson('http://localhost:8000/api/delete', {
                method: 'POST', headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ file_paths: files })
            });
            alert(`Success! Deleted ${res.deleted.length} files.`);
            document.getElementById(clickEventBtn).click(); // Auto-refresh the specific list
        } catch (error) { alert("Deletion Error: " + error.message); }
    };

    document.getElementById('executeHogsBtn').addEventListener('click', () => handleRadarDelete('executeHogsBtn', 'scanHogsBtn'));
    document.getElementById('executeStaleBtn').addEventListener('click', () => handleRadarDelete('executeStaleBtn', 'scanStaleBtn'));

    //Make the global Set Target button refresh the current active tab
    document.getElementById('globalScanBtn').addEventListener('click', () => {
        const activeTabBtn = document.querySelector('.tab-btn.active');
        if (activeTabBtn.dataset.target === 'tab-mapper') document.getElementById('runMapperBtn').click();
        if (activeTabBtn.dataset.target === 'tab-dedup') document.getElementById('runDedupBtn').click();
        if (activeTabBtn.dataset.target === 'tab-radar') {
            document.getElementById('scanHogsBtn').click();
            document.getElementById('scanStaleBtn').click();
        }
    });