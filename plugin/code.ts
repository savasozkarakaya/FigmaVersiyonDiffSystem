figma.showUI(__html__, { width: 300, height: 500 });

// Load settings on start
figma.clientStorage.getAsync('settings').then(settings => {
    if (settings) {
        figma.ui.postMessage({ type: 'load-settings', settings });
    }
});

figma.ui.onmessage = async (msg) => {
    if (msg.type === 'save-settings') {
        await figma.clientStorage.setAsync('settings', msg.settings);
    }
    else if (msg.type === 'capture') {
        await captureBaseline(msg.issueKey);
    }
    else if (msg.type === 'compare') {
        await compareNodes(msg.issueKey, msg.slackChannel);
    }
    else if (msg.type === 'baseline-success') {
        // Backend saved it, now we save metadata on node
        const node = figma.getNodeById(msg.nodeId);
        if (node) {
            node.setPluginData('baselineId', msg.baselineId);
            node.setPluginData('lastBaselineAt', new Date().toISOString());
            figma.notify("Baseline metadata updated on node");
        }
    }
};

async function captureBaseline(issueKey: string) {
    const selection = figma.currentPage.selection;
    if (selection.length === 0) {
        figma.notify("Please select at least one frame");
        return;
    }

    for (const node of selection) {
        try {
            // Export
            const bytes = await node.exportAsync({ format: 'PNG', constraint: { type: 'SCALE', value: 2 } });

            const metadata = {
                issueKey,
                nodeId: node.id,
                nodeName: node.name,
                fileKey: figma.fileKey,
                pageName: figma.currentPage.name,
                user: figma.currentUser?.name || "Unknown",
                structureJson: JSON.stringify({ w: node.width, h: node.height, type: node.type })
            };

            // Send to UI to upload
            figma.ui.postMessage({
                type: 'upload-baseline',
                data: { bytes, metadata }
            });

        } catch (err) {
            console.error(err);
            figma.ui.postMessage({ type: 'log', message: `Error capturing ${node.name}`, isError: true });
        }
    }
}

async function compareNodes(issueKey: string, slackChannel: string) {
    const selection = figma.currentPage.selection;
    if (selection.length === 0) {
        figma.notify("Select nodes to compare");
        return;
    }

    for (const node of selection) {
        const baselineId = node.getPluginData('baselineId');
        if (!baselineId) {
            figma.ui.postMessage({ type: 'log', message: `${node.name}: No baseline found`, isError: true });
            continue;
        }

        try {
            const bytes = await node.exportAsync({ format: 'PNG', constraint: { type: 'SCALE', value: 2 } });

            const metadata = {
                baselineId, // Important!
                issueKey,
                slackChannel,
                nodeId: node.id,
                nodeName: node.name
            };

            figma.ui.postMessage({
                type: 'upload-comparison',
                data: { bytes, metadata }
            });

        } catch (err) {
            figma.ui.postMessage({ type: 'log', message: `Export error ${node.name}`, isError: true });
        }
    }
}
