﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Caching;
using System.Web.Hosting;
using System.Web.Optimization;
using SmartStore.Core;
using SmartStore.Core.Caching;
using SmartStore.Core.Infrastructure;

namespace SmartStore.Web.Framework.Themes
{
    public class ThemingVirtualPathProvider : VirtualPathProvider
    {
		private readonly VirtualPathProvider _previous;

        public ThemingVirtualPathProvider(VirtualPathProvider previous)
        {
            _previous = previous;
        }

        public override bool FileExists(string virtualPath)
        {
			if (ThemeHelper.PathIsThemeVars(virtualPath))
			{
				return true;
			}

			var result = GetResolveResult(virtualPath);
			if (result != null)
			{
				return true;
			}

			return _previous.FileExists(virtualPath);
        }
         
        public override VirtualFile GetFile(string virtualPath)
        {
			if (ThemeHelper.PathIsThemeVars(virtualPath))
			{
				var theme = ThemeHelper.ResolveCurrentTheme();
				int storeId = ThemeHelper.ResolveCurrentStoreId();
				return new ThemeVarsVirtualFile(virtualPath, theme.ThemeName, storeId);
			}

			var result = GetResolveResult(virtualPath);
			if (result != null)
			{
				return new InheritedVirtualThemeFile(result);
			}

            return _previous.GetFile(virtualPath);
        }
        
        public override CacheDependency GetCacheDependency(string virtualPath, IEnumerable virtualPathDependencies, DateTime utcStart)
        {

            bool isLess;
			if (!IsStyleSheet(virtualPath, out isLess))
			{
				return GetCacheDependencyInternal(virtualPath, virtualPathDependencies, utcStart);
			}
            else
            {
                if (!isLess)
                {
					// the Bundler made the call (NOT the LESS HTTP handler)
					var bundle = BundleTable.Bundles.GetBundleFor(virtualPath);
					if (bundle == null)
					{
						return GetCacheDependencyInternal(virtualPath, virtualPathDependencies, utcStart);
					}
                }
                
                var arrPathDependencies = virtualPathDependencies.Cast<string>().ToArray();

                // determine the virtual themevars.less import reference
                var themeVarsFile = arrPathDependencies.Where(x => ThemeHelper.PathIsThemeVars(x)).FirstOrDefault();

                if (themeVarsFile.IsEmpty())
                {
                    // no themevars import... so no special considerations here
					return GetCacheDependencyInternal(virtualPath, virtualPathDependencies, utcStart);
                }

                // exclude the themevars import from the file dependencies list,
                // 'cause this one cannot be monitored by the physical file system
                var fileDependencies = arrPathDependencies.Except(new string[] { themeVarsFile });

                if (arrPathDependencies.Any())
                {
                    int storeId = ThemeHelper.ResolveCurrentStoreId();
                    var theme = ThemeHelper.ResolveCurrentTheme();
                    // invalidate the cache when variables change
                    string cacheKey = AspNetCache.BuildKey(FrameworkCacheConsumer.BuildThemeVarsCacheKey(theme.ThemeName, storeId));
					var cacheDependency = new CacheDependency(MapDependencyPaths(fileDependencies), new string[] { cacheKey }, utcStart);
                    return cacheDependency;
                }

                return null;
            }
        }

		private CacheDependency GetCacheDependencyInternal(string virtualPath, IEnumerable virtualPathDependencies, DateTime utcStart)
		{
			return new CacheDependency(MapDependencyPaths(virtualPathDependencies.Cast<string>()), utcStart);
		}

		private string[] MapDependencyPaths(IEnumerable<string> virtualPathDependencies)
		{
			var fileNames = new List<string>();

			foreach (var dep in virtualPathDependencies)
			{
				var result = GetResolveResult(dep);
				if (result != null)
				{
					fileNames.Add(result.ResultPhysicalPath);
				}
				else
				{
					fileNames.Add(HostingEnvironment.MapPath(dep));
				}
			}

			return fileNames.ToArray();
		}

		private InheritedThemeFileResult GetResolveResult(string virtualPath)
		{
			var resolver = EngineContext.Current.Resolve<IThemeFileResolver>();
			var result = resolver.Resolve(virtualPath);
			return result;
		}

        private static bool IsStyleSheet(string virtualPath, out bool isLess)
        {
            bool isCss = false;
            isLess = virtualPath.EndsWith(".less", StringComparison.OrdinalIgnoreCase);
            if (!isLess)
                isCss = virtualPath.EndsWith(".css", StringComparison.OrdinalIgnoreCase);
            return isLess || isCss;
        }

    }
}