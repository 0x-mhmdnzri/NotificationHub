function readPackage(pkg) {
  if (pkg.dependencies?.postcss) {
    pkg.dependencies.postcss = '8.5.26'
  }
  if (pkg.devDependencies?.postcss) {
    pkg.devDependencies.postcss = '8.5.26'
  }
  if (pkg.optionalDependencies?.postcss) {
    pkg.optionalDependencies.postcss = '8.5.26'
  }
  return pkg
}
module.exports = { hooks: { readPackage } }
