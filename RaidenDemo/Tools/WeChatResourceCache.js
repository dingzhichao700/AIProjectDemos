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
    // 共享 Bundle 和版本 Catalog 不可变；发布入口及 settings 始终联网。
    if (relativePath.split('/').some(part => part === '.' || part === '..')) {
        return false;
    }
    return /^\d{8}T\d{9}Z\/bundles\/.+_[0-9a-f]{16,64}\.bundle$/.test(relativePath)
        || /^\d{8}T\d{9}Z\/releases\/\d{8}T\d{9}Z\/catalog\.json$/.test(relativePath);
};
