![](https://upload-images.jianshu.io/upload_images/2799767-393d1e8c64630875.png?imageMogr2/auto-orient/strip%7CimageView2/2/w/1240)


# 前言
玩K8S也有一段时间了，借助云服务提供商的K8S控制台，已经可以很方便的快速部署应用至K8S。通过简单的点击，可以一次性帮忙创建K8S 对象：Deployment、Service、Ingress、ConfigMap等。但是当服务的规模上来后，这种方式就有点捉襟见肘。尤其是需要同时更新多个关联服务时，就需要一个一个的去更改，就有点不太方便。为了解决这个问题，最近上手实操了一下Helm，发现生产力大大提升。

# Helm 简介
[Helm](https://helm.sh/) 是一个为K8S打造的包管理器。通过Helm可以方便管理Kubernetes应用程序。Helm主要有两大核心概念：Charts、Release。
1. Chart：用来定义，安装和升级K8S 应用。亦可分享及版本化控制。
2. Release：类似Image之于Container，Release是Chart的运行实例。

目前Helm最新的版本为V3.1，较之前版本，在整体架构上移除服务端Tiller。
对于Windows系统而言可借助Choco快速安装：`choco install kubernetes-helm`，通过执行`helm version`确认是否安装成功。
`version.BuildInfo{Version:"v3.1.0", GitCommit:"b29d20baf09943e134c2fa5e1e1cab3bf93315fa", GitTreeState:"clean", GoVersion:"go1.13.7"}`

在继续往前，请确保已具备基础的K8S基础知识，并且确保本机已安装Docker和K8S。安装教程和K8S简单入门可参考我的这篇文章[ASP.NET Core 借助 K8S 玩转容器编排](https://www.cnblogs.com/sheng-jie/p/10591794.html)。

对于第一次接触Helm .NETer 来说我们可以通过VS 2019来快速体验一下。请确保已安装Visual Studio Tools for Kubernetes。
![](https://upload-images.jianshu.io/upload_images/2799767-616ec726bf04562d.png?imageMogr2/auto-orient/strip%7CimageView2/2/w/1240)

# 创建 Chart (helm create)

打开VS 创建项目，选择**Container Application for Kubernetes**，创建一个空的ASP.NET Core Web 项目。
![](https://upload-images.jianshu.io/upload_images/2799767-01a2cb3bcd7a0247.png?imageMogr2/auto-orient/strip%7CimageView2/2/w/1240)
创建后，项目结构如下图所示，与平时之间创建的Web项目而言，主要是多了一个`charts`目录、`Dockerfile`和一个`azds.yaml`。
![项目结构](https://upload-images.jianshu.io/upload_images/2799767-899241f45ad5d4de.png?imageMogr2/auto-orient/strip%7CimageView2/2/w/1240)
除了创建项目时通过选择**Container Application for Kubernetes**类型外，我们也可以通过其他方式创建。我们这里手动删除`charts`目录、`Dockerfile`和一个`azds.yaml`。然后如下图步骤即可重新生成Helm Chart。
![添加 helm chart](https://upload-images.jianshu.io/upload_images/2799767-48decb7ce8c3a6bc.png?imageMogr2/auto-orient/strip%7CimageView2/2/w/1240)

当然也可以通过`helm create`创建。

# 安装 Chart (helm install)

在展开之前，先来简要介绍Chart目录：
```
k8shelmdemo/                         # Chart 目录
├── charts                           # 这个 charts 依赖的其他 charts，始终被安装
├── Chart.yaml                       # 描述这个 Chart 的相关信息、包括名字、描述信息、版本等
├── templates                        # 模板目录
│   ├── deployment.yaml              # deployment 控制器的 Go 模板文件
│   ├── _helpers.tpl                 # 以 _ 开头的文件不会部署到 k8s 上，可用于定制通用信息
│   ├── ingress.yaml                 # ingress 的模板文件
│   ├── NOTES.txt                    # Chart 帮助文本，安装后会显示给用户，例如：如何使用、列出缺省值
│   ├── service.yaml                 # service 的 Go 模板文件
│   ├── secrets.yaml                 # secrets 的 Go 模板文件
│   └── tests
│       └── test-connection.yaml
└── values.yaml                      # 模板的值文件，这些值会在安装时应用到 GO 模板生成部署文件
```
简单来说，Helm Chart 定义常用的K8S 对象模板，通过values.yaml来填充模板。那我们就来看看填充后的输出结果是怎样的。打开命令提示符，进入到Chart目录，通过`helm template --debug [release name] [chart dir]`命令就可以测试Chart（亦可通过`helm --dry-run --debug [release name] [chart dir]`测试）。可以看到输出了Service和Deployment。这里你可能就纳闷了，不是定义了4个K8S对象模板吗，为什么就是输出2个yaml文件呢。这里先按住不表。

```
PS \K8S.Helm.Demo\charts> helm template --debug k8s-helm-demo .\k8shelmdemo\
install.go:158: [debug] Original chart version: ""
install.go:175: [debug] CHART PATH: \K8S.Helm.Demo\charts\k8shelmdemo
---
# Source: k8shelmdemo/templates/service.yaml
apiVersion: v1
kind: Service
metadata:
  name: k8shelmdemo
  labels:
    app: k8shelmdemo
    chart: k8shelmdemo-0.1.0
    release: k8s-helm-demo
    heritage: Helm
spec:
  type: ClusterIP
  ports:
    - port: 80
      targetPort: http
      protocol: TCP
      name: http
  selector:
    app: k8shelmdemo
    release: k8s-helm-demo
---
# Source: k8shelmdemo/templates/deployment.yaml
apiVersion: apps/v1beta2
kind: Deployment
metadata:
  name: k8shelmdemo
  labels:
    app: k8shelmdemo
    chart: k8shelmdemo-0.1.0
    draft: draft-app
    release: k8s-helm-demo
    heritage: Helm
spec:
  replicas: 1
  selector:
    matchLabels:
      app: k8shelmdemo
      release: k8s-helm-demo
  template:
    metadata:
      labels:
        app: k8shelmdemo
        draft: draft-app
        release: k8s-helm-demo
      annotations:
        buildID: ""
    spec:
      containers:
        - name: k8shelmdemo
          image: "k8shelmdemo:stable"
          imagePullPolicy: IfNotPresent
          ports:
            - name: http
              containerPort: 80
              protocol: TCP
          env:
          resources:
            {}
```
这里目前需要注意一点就是上面输出的Deployment中使用的镜像为**`image: "k8shelmdemo:stable"`**。所以在安装该Chart之前，我们需要构造镜像。镜像构造很简单，在Vs中右键Dockerfile选择构建就好（请确保Docker已启动）。
![build docker image](https://upload-images.jianshu.io/upload_images/2799767-ba0c3d5f6b4b61d1.png?imageMogr2/auto-orient/strip%7CimageView2/2/w/1240)
观察VS输出窗口，会有以下输出：
```1>------ Rebuild All started: Project: K8S.Helm.Demo, Configuration: Debug Any CPU ------
1>K8S.Helm.Demo -> D:\Programming\Coding\dotnet\K8S.Ocelot.Demo\src\K8S.Helm.Demo\bin\Debug\netcoreapp3.1\K8S.Helm.Demo.dll
1>Docker version 19.03.1, build 74b1e89
1>docker build -f "d:\programming\coding\dotnet\k8s.ocelot.demo\src\k8s.helm.demo\dockerfile" --force-rm -t k8shelmdemo  --label "com.microsoft.created-by=visual-studio" --label "com.microsoft.visual-studio.project-name=K8S.Helm.Demo" "d:\programming\coding\dotnet\k8s.ocelot.demo\src"
1>Sending build context to Docker daemon  6.192MB
1>
1>Step 1/18 : FROM mcr.microsoft.com/dotnet/core/aspnet:3.1-buster-slim AS base
1>Step 2/18 : WORKDIR /app
1> ---> e28362768eed
1> ---> Using cache
1> ---> 6457841dbdf1
1>Step 3/18 : EXPOSE 80
1> ---> Using cache
1> ---> bb9dc51530fe
1>Step 4/18 : FROM mcr.microsoft.com/dotnet/core/sdk:3.1-buster AS build
1> ---> 0a4c7c13f9d6
1>Step 5/18 : WORKDIR /src
1> ---> Using cache
1> ---> ddc36742b71c
1>Step 6/18 : COPY ["K8S.Helm.Demo/K8S.Helm.Demo.csproj", "K8S.Helm.Demo/"]
1> ---> d18831c83acd
1>Step 7/18 : RUN dotnet restore "K8S.Helm.Demo/K8S.Helm.Demo.csproj"
1> ---> Running in 64d3624eb9c0
1>  Restore completed in 7.19 sec for /src/K8S.Helm.Demo/K8S.Helm.Demo.csproj.
1>Removing intermediate container 64d3624eb9c0
1> ---> ba3624442138
1>Step 8/18 : COPY . .
1> ---> 43c4f6c4769f
1>Step 9/18 : WORKDIR "/src/K8S.Helm.Demo"
1> ---> Running in 145e155d3a5d
1>Removing intermediate container 145e155d3a5d
1>Step 10/18 : RUN dotnet build "K8S.Helm.Demo.csproj" -c Release -o /app/build
1> ---> e547e8caed4a
1> ---> Running in 146df981f291
1>Microsoft (R) Build Engine version 16.4.0+e901037fe for .NET Core
1>Copyright (C) Microsoft Corporation. All rights reserved.
1>
1>  Restore completed in 27.08 ms for /src/K8S.Helm.Demo/K8S.Helm.Demo.csproj.
1>  K8S.Helm.Demo -> /app/build/K8S.Helm.Demo.dll
1>Build succeeded.
1>    0 Warning(s)
1>
1>Time Elapsed 00:00:02.09
1>    0 Error(s)
1>Removing intermediate container 146df981f291
1>Step 11/18 : FROM build AS publish
1> ---> 94f07ad82c1c
1> ---> 94f07ad82c1c
1>Step 12/18 : RUN dotnet publish "K8S.Helm.Demo.csproj" -c Release -o /app/publish
1> ---> Running in 60d63984fe28
1>Microsoft (R) Build Engine version 16.4.0+e901037fe for .NET Core
1>
1>Copyright (C) Microsoft Corporation. All rights reserved.
1>  Restore completed in 26.94 ms for /src/K8S.Helm.Demo/K8S.Helm.Demo.csproj.
1>  K8S.Helm.Demo -> /src/K8S.Helm.Demo/bin/Release/netcoreapp3.1/K8S.Helm.Demo.dll
1>  K8S.Helm.Demo -> /app/publish/
1>Removing intermediate container 60d63984fe28
1>Step 13/18 : FROM base AS final
1> ---> 85d893dc4a81
1> ---> bb9dc51530fe
1>Step 14/18 : WORKDIR /app
1> ---> Running in 69b7fd56c371
1>Removing intermediate container 69b7fd56c371
1> ---> 219310025c54
1>Step 15/18 : COPY --from=publish /app/publish .
1>Step 16/18 : ENTRYPOINT ["dotnet", "K8S.Helm.Demo.dll"]
1> ---> 6e63a4449dbb
1> ---> Running in a43a0516c6dc
1>Removing intermediate container a43a0516c6dc
1> ---> 36f422c923fd
1>Step 17/18 : LABEL com.microsoft.created-by=visual-studio
1> ---> Running in 88d100227ee1
1>Removing intermediate container 88d100227ee1
1> ---> 4a71f8e5e761
1>Step 18/18 : LABEL com.microsoft.visual-studio.project-name=K8S.Helm.Demo
1> ---> Running in f609881010ad
1>Removing intermediate container f609881010ad
1> ---> 3301427c0fb8
1>Successfully built 3301427c0fb8
1>Successfully tagged k8shelmdemo:latest
1>SECURITY WARNING: You are building a Docker image from Windows against a non-Windows Docker host. All files and directories added to build context will have '-rwxr-xr-x' permissions. It is recommended to double check and reset permissions for sensitive files and directories.
========== Rebuild All: 1 succeeded, 0 failed, 0 skipped ==========
```
最终构建的镜像名称为`k8shelmdemo:latest`。与我们上面Chart中使用的镜像`k8shelmdemo:stable`不一致。如果现在安装Chart，那么应用将无法找对应的镜像无法启动。那怎么办呢。查看`deployment.yaml`模板文件，我们发现其镜像引用定义为`image: "{{ .Values.image.repository }}:{{ .Values.image.tag }}"`。查看`values.yaml`发现如下定义：
```
image:
  repository: k8shelmdemo
  tag: stable
  pullPolicy: IfNotPresent
```
所以改法就很简单了，将`values.yaml`的tag更改为`latest`即可。更改后在执行`helm template --debug [release name] [chart dir]` 验证下。接下来通过`helm install`来安装Chart。在执行之前，我们先通过`kubectl create ns helmdemo`创建一个独立的命名空间以方便确认和清理。再执行`helm install k8shelmdemo .\k8shelmdemo\ -n helmdemo` 安装（-n 指定我们刚刚创建的命名空间）。具体命令如下：

```
PS \K8S.Helm.Demo\charts> kubectl create ns helmdemo
namespace/helmdemo created
PS \K8S.Helm.Demo\charts> helm install k8shelmdemo .\k8shelmdemo\ -n helmdemo
NAME: k8shelmdemo
LAST DEPLOYED: Sun Feb 23 17:33:54 2020
NAMESPACE: helmdemo
STATUS: deployed
REVISION: 1
TEST SUITE: None
NOTES:
1. Get the application URL by running these commands:
  export POD_NAME=$(kubectl get pods --namespace helmdemo -l "app=k8shelmdemo,release=k8shelmdemo" -o jsonpath="{.items[0].metadat
a.name}")
  echo "Visit http://127.0.0.1:8080 to use your application"
  kubectl port-forward $POD_NAME 8080:80
PS \K8S.Helm.Demo\charts>
```
我们看到同时输出了模板文件夹下的NOTES模板，这时说明helm已经安装了。那怎样确保是否安装成功了呢。继续执行以下命令：
```
PS \K8S.Helm.Demo\charts> helm list -n helmdemo #查看指定命名空间下已安装的chart，也就是运行中的Release
NAME            NAMESPACE       REVISION        UPDATED                                 STATUS          CHART                   APP VERSION
k8shelmdemo     helmdemo        1               2020-02-23 17:33:54.2196357 +0800 CST   deployed        k8shelmdemo-0.1.0       1.0
PS \K8S.Helm.Demo\charts> kubectl get all -n helmdemo # 查看指定命名空间下K8S下所有的对象
NAME                               READY   STATUS    RESTARTS   AGE
pod/k8shelmdemo-689bd54677-fcfx7   1/1     Running   0          5m8s

NAME                  TYPE        CLUSTER-IP      EXTERNAL-IP   PORT(S)   AGE
service/k8shelmdemo   ClusterIP   10.97.204.227   <none>        80/TCP    5m8s

NAME                          READY   UP-TO-DATE   AVAILABLE   AGE
deployment.apps/k8shelmdemo   1/1     1            1           5m8s

NAME                                     DESIRED   CURRENT   READY   AGE
replicaset.apps/k8shelmdemo-689bd54677   1         1         1       5m8s
```
至此我们可以确定Chart成功安装。那如何访问刚刚部署的Web应用呢，安装刚刚Chart的安装Notes，通过`kubectl port-forward`配置端口转发，来完成。从上面的输出我们已经知道对应的Pod Name 为k8shelmdemo-689bd54677-fcfx7。执行`kubectl port-forward k8shelmdemo-689bd54677-fcfx7 8090:80`。
```
PS \K8S.Helm.Demo\charts> kubectl port-forward k8shelmdemo-689bd54677-fcfx7 8090:80 -n helmdemo
Forwarding from 127.0.0.1:8090 -> 80
Forwarding from [::1]:8090 -> 80
```
换一个命令行执行`curl -l http://localhost:8090` 会看到输出`Hello world`。

# 更新 Chart (helm upgrade)
假设我现在想将输出更新为`Hello Helm`，我们来看下怎么办。对于当前应用来说，更新输出，只需要更改Startup的`Hello World`改为`Hello Helm` 就好，然后重新构建镜像。
这里思考一下，因为重新构建的镜像Tag还是`k8shelmdemo:latest`，所以无需对当前Helm Chart做任何改动，所以也就无需更新。那我们该如何更新应用呢。如果有K8S基础的同学应该很快就能想到，直接删除Pod即可。因为从上面`kubectl get all -n helmdemo`的输出中，我们可以看到Chart为我们的应用自动创建了一个`ReplicaSet`实例，ReplicaSet主要用于确保应用始终保持指定数量的实例运行。所以如果删除一个Pod，K8S会按照ReplicaSet的定义，重新启用一个新的Pod。再重新执行`kubectl port-forward`，会发现应用已更新。
> PS: 因为当前demo使用的是本地镜像，所以删除Pod后，重新运行的pod能够输出更新后的结果。如果镜像来源并非本地，那么对于同一个镜像tag来说，就要考虑更新镜像拉取策略。
```
PS \K8S.Helm.Demo\charts> kubectl delete pod/k8shelmdemo-689bd54677-fcfx7 -n helmdemo # 删除pod
PS \K8S.Helm.Demo\charts> kubectl get all -n helmdemo
NAME                               READY   STATUS    RESTARTS   AGE
pod/k8shelmdemo-689bd54677-mrr64   1/1     Running   0          29s # 新的Pod创建成功

NAME                  TYPE        CLUSTER-IP      EXTERNAL-IP   PORT(S)   AGE
service/k8shelmdemo   ClusterIP   10.97.204.227   <none>        80/TCP    33m

NAME                          READY   UP-TO-DATE   AVAILABLE   AGE
deployment.apps/k8shelmdemo   1/1     1            1           33m

NAME                                     DESIRED   CURRENT   READY   AGE
replicaset.apps/k8shelmdemo-689bd54677   1         1         1       33m
```
假设现在需要服务部署后直接通过指定端口访问，无需通过`kubeclt port-forward`进行端口转发。那么我们就需要对Chart进行相应更新，将生成的Service的类型由默认的`Cluster`更改为`LoadBalancer`模式。更新`values.yaml`中的service节点如下：
```
service:
  type: LoadBalancer
  port: 8093
```
紧接着通过执行`helm upgrade [release name] [chart dir]`命令更新应用，如下。
```
PS \K8S.Helm.Demo\charts> helm upgrade k8shelmdemo .\k8shelmdemo\ -n helmdemo
Release "k8shelmdemo" has been upgraded. Happy Helming!
NAME: k8shelmdemo
LAST DEPLOYED: Sun Feb 23 18:39:30 2020
NAMESPACE: helmdemo
STATUS: deployed
REVISION: 3
TEST SUITE: None
NOTES:
1. Get the application URL by running these commands:
     NOTE: It may take a few minutes for the LoadBalancer IP to be available.
           You can watch the status of by running 'kubectl get svc -w k8shelmdemo'
  export SERVICE_IP=$(kubectl get svc --namespace helmdemo k8shelmdemo -o jsonpath='{.status.loadBalancer.ingress[0].ip}')
  echo http://$SERVICE_IP:8093
PS \K8S.Helm.Demo\charts> kubectl get all -n helmdemo
NAME                               READY   STATUS    RESTARTS   AGE
pod/k8shelmdemo-689bd54677-pj5pd   1/1     Running   0          22m

NAME                  TYPE           CLUSTER-IP      EXTERNAL-IP   PORT(S)          AGE
service/k8shelmdemo   LoadBalancer   10.97.204.227   localhost     8093:30035/TCP   65m

NAME                          READY   UP-TO-DATE   AVAILABLE   AGE
deployment.apps/k8shelmdemo   1/1     1            1           65m

NAME                                     DESIRED   CURRENT   READY   AGE
replicaset.apps/k8shelmdemo-689bd54677   1         1         1       65m
PS \K8S.Ocelot.Demo\src\K8S.Helm.Demo\charts> curl -l localhost:8093
Hello Helm!
PS \K8S.Helm.Demo\charts> helm ls -n helmdemo # 版本已更新
NAME            NAMESPACE       REVISION        UPDATED                                 STATUS          CHART                   APP VERSION
k8shelmdemo     helmdemo        2                2020-02-23 18:39:30.2059395 +0800 CST   deployed        k8shelmdemo-0.1.0       1.0
```
# 删除 Chart (helm delete)
演示完毕，那如何删除已发布的Release呢，执行`helm delete k8shelmdemo -n helmdemo`。
```
PS \K8S.Helm.Demo\charts> helm delete k8shelmdemo -n helmdemo
release "k8shelmdemo" uninstalled
PS \K8S.Helm.Demo\charts> helm ls -n helmdemo
NAME    NAMESPACE       REVISION        UPDATED STATUS  CHART   APP VERSION
PS \K8S.Helm.Demo\charts> kubectl get all -n helmdemo
No resources found.
```
执行后，可以发现创建的K8S资源也已清理干净。

# 最后
以上仅是对 ASP.NET Core 如何使用 Helm 部署到K8S的简单介绍，希望对入门的你有所帮助！对于Helm复杂的应用，主要在于模板填充的复杂应用，大家可以结合[官方Helm文档](https://helm.sh/docs/chart_template_guide/)以及[eShopOnContainer中Helm示例](https://github.com/dotnet-architecture/eShopOnContainers/tree/dev/deploy/k8s/helm)进行学习。




> 参考资料：
[Get started with Visual Studio Kubernetes Tools](https://docs.microsoft.com/en-us/visualstudio/containers/tutorial-kubernetes-tools?view=vs-2019)
[玩K8S不得不会的HELM](https://juejin.im/post/5d5b8dba6fb9a06b1b19c21b#heading-25)

![](https://upload-images.jianshu.io/upload_images/2799767-8e2d6e194074711f.png?imageMogr2/auto-orient/strip%7CimageView2/2/w/1240)




