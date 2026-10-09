// 由微信导出流程写入 unity-namespace.js，与版本目录发布方式配套。
unityNamespace.isCacheableFile = function (path) {
    // 保留开发者工具的二进制缓存兼容处理。
    if (wx.getSystemInfoSync().platform === 'devtools' || typeof path !== 'string') {
        return false;
    }
    const cdn = GameGlobal.managerConfig && GameGlobal.managerConfig.DATA_CDN;
    if (!cdn) {
        return false;
    }
    const prefix = cdn.replace(/\/+$/, '') + '/';
    if (!path.startsWith(prefix)) {
        return false;
    }
    const relativePath = path.substring(prefix.length).split(/[?#]/)[0];
    // 仅缓存不可变的“底座版本/资源版本”内容；可变发布入口始终联网。
    const versionPath = /^\d{8}T\d{9}Z\/\d{8}T\d{9}Z\/(.+)$/.exec(relativePath);
    if (!versionPath) {
        return false;
    }
    const resource = versionPath[1];
    if (resource.split('/').some(part => part === '.' || part === '..')) {
        return false;
    }
    return resource.endsWith('.bundle') || /^catalog(?:_[^/]*)?\.json$/.test(resource);
};
