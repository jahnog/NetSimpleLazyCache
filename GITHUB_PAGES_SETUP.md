# GitHub Pages Coverage Setup

This document explains how to access the coverage reports deployed to GitHub Pages.

## 🌐 Online Coverage Reports

The coverage reports are automatically deployed to GitHub Pages after each successful coverage workflow run on the main branch.

### Access URLs

**Main Coverage Dashboard**: https://jahnog.github.io/NetSimpleLazyCache/

**Direct Report Links**:
- 📊 **Interactive HTML Report**: https://jahnog.github.io/NetSimpleLazyCache/report/
- 📝 **Markdown Summary**: https://jahnog.github.io/NetSimpleLazyCache/report/Summary.md
- 🐙 **GitHub Markdown**: https://jahnog.github.io/NetSimpleLazyCache/report/SummaryGithub.md

### What's Available

The GitHub Pages site includes:
- **Coverage Dashboard**: Overview with key metrics
- **Interactive HTML Reports**: Detailed line-by-line coverage
- **Markdown Reports**: Text-based summaries
- **Historical Data**: Coverage trends over time
- **Direct Downloads**: Access to raw coverage files

## 🔧 Setup Instructions

### 1. Enable GitHub Pages

In your repository settings:
1. Go to **Settings** → **Pages**
2. Under **Source**, select **GitHub Actions**
3. Save the settings

### 2. Workflow Configuration

The setup includes two workflows:

#### Coverage Workflow (`.github/workflows/coverage.yml`)
- Runs tests and generates coverage reports
- Uploads reports as artifacts
- Updates coverage badges

#### Pages Deployment (`.github/workflows/deploy-coverage.yml`)
- Triggers after successful coverage workflow
- Downloads coverage artifacts
- Deploys to GitHub Pages

### 3. First Deployment

After enabling GitHub Pages and pushing the workflows:
1. Push to main branch
2. Wait for coverage workflow to complete
3. Pages deployment will trigger automatically
4. Site will be available at your GitHub Pages URL

## 📊 Features

### Coverage Dashboard
- **Visual Overview**: Key coverage metrics at a glance
- **Quick Navigation**: Direct links to detailed reports
- **Professional Design**: Clean, responsive interface

### Interactive Reports
- **Line-by-line Coverage**: See exactly which lines are covered
- **Branch Analysis**: Detailed branch coverage information
- **File Navigation**: Easy browsing through source files
- **Search Functionality**: Find specific files or functions

### Automatic Updates
- **Continuous Deployment**: Updates on every main branch push
- **Version History**: GitHub Pages maintains deployment history
- **Fast Updates**: Reports available within minutes of code changes

## 🔗 Integration

### README Badges
Update your README.md badges to link to the online reports:

```markdown
[![Line Coverage](https://jahnog.github.io/NetSimpleLazyCache/badge_linecoverage.svg)](https://jahnog.github.io/NetSimpleLazyCache/)
[![Branch Coverage](https://jahnog.github.io/NetSimpleLazyCache/badge_branchcoverage.svg)](https://jahnog.github.io/NetSimpleLazyCache/)
[![Method Coverage](https://jahnog.github.io/NetSimpleLazyCache/badge_methodcoverage.svg)](https://jahnog.github.io/NetSimpleLazyCache/)
```

### Status Checks
The coverage workflow provides:
- **GitHub Actions Summary**: Quick coverage overview
- **Artifact Downloads**: Full report packages
- **GitHub Pages Links**: Direct access to online reports

## 🚀 Benefits

✅ **Always Accessible**: No need to download artifacts  
✅ **Professional Presentation**: Clean, branded coverage reports  
✅ **Fast Loading**: Optimized for quick access  
✅ **Mobile Friendly**: Responsive design works on all devices  
✅ **SEO Friendly**: Proper HTML structure and metadata  
✅ **Version History**: GitHub Pages maintains deployment history  

## 🛠️ Troubleshooting

### Pages Not Deploying
1. Check GitHub Pages is enabled in repository settings
2. Verify workflows have proper permissions
3. Ensure main branch protection allows GitHub Actions

### Reports Not Updating
1. Check coverage workflow completed successfully
2. Verify artifacts were uploaded
3. Check pages deployment workflow logs

### Access Issues
1. Verify repository is public (for public GitHub Pages)
2. Check GitHub Pages settings
3. Ensure proper URL format

The GitHub Pages setup provides a professional, always-accessible way to view your coverage reports!
