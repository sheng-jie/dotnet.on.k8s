![](https://upload-images.jianshu.io/upload_images/2799767-b660dbd059b8b2b5.png?imageMogr2/auto-orient/strip%7CimageView2/2/w/1240)

# 背景
ASP.NET Core默认的配置文件定义在`appsetings.json`和`appsettings.{Environment}.json`文件中。
这里面有一个问题就是，在使用容器部署时，每次修改配置文件都需要重新构建镜像。当然你也可能会说，我的配置文件很稳定不需要修改，但你又如何确保配置文件中一些机密配置的安全问题呢？比如暴露了你的远程数据库的连接信息，哪天被员工不小心删库跑路了呢？
那接下来就来讲解下如何在.NET Core 中正确使用ConfigMap。

# ConfigMap/Secret
K8S中引入了ConfigMap/Secret来存储配置数据，分别用于存储非敏感信息和敏感信息。其目的在于将应用和配置解耦，以确保容器化应用程序的可移植性。
                                              
# 创建 ConfigMap
玩耍K8S，请先自行准备环境，Win10用户可以参考我的上篇文章[ASP.NET Core 借助 K8S 玩转容器编排](https://www.jianshu.com/p/6d39e935e66f)来准备环境。

ConfigMap的创建很简单，一句命令就可以直接将`appsettings.json`文件转换为ConfigMap。

*PS:使用K8S一定要善用帮助命令，比如执行`kubectl create configmap -h`，你就可以了解到多种创建ConfigMap的方式。*

```
> kubectl create configmap -h
Create a configmap based on a file, directory, or specified literal value.

A single configmap may package one or more key/value pairs.

When creating a configmap based on a file, the key will default to the basename of the file, and the value will default
to the file content.  If the basename is an invalid key, you may specify an alternate key.

When creating a configmap based on a directory, each file whose basename is a valid key in the directory will be
packaged into the configmap.  Any directory entries except regular files are ignored (e.g. subdirectories, symlinks,
devices, pipes, etc).

Aliases:
configmap, cm

Examples:
  # Create a new configmap named my-config based on folder bar
  kubectl create configmap my-config --from-file=path/to/bar

  # Create a new configmap named my-config with specified keys instead of file basenames on disk
  kubectl create configmap my-config --from-file=key1=/path/to/bar/file1.txt --from-file=key2=/path/to/bar/file2.txt

  # Create a new configmap named my-config with key1=config1 and key2=config2
  kubectl create configmap my-config --from-literal=key1=config1 --from-literal=key2=config2

  # Create a new configmap named my-config from the key=value pairs in the file
  kubectl create configmap my-config --from-file=path/to/bar

  # Create a new configmap named my-config from an env file
  kubectl create configmap my-config --from-env-file=path/to/bar.env
```
其中我们可以看到可以通过指定`--from-file`来从指定文件创建。
```
kubectl create configmap my-config --from-file=key1=/path/to/bar/file1.txt --from-file=key2=/path/to/bar/file2.txt
```
Let's have a try!

**1.** 先行创建示例项目：`dotnet new mvc -n K8S.NETCore.ConfigMap`
**2.** 默认包含两个配置文件`appsettings.json`和`appsettings.Development.json`
![](https://upload-images.jianshu.io/upload_images/2799767-ce299d630488d4e2.png?imageMogr2/auto-orient/strip%7CimageView2/2/w/1240)

**3.** 先来尝试将`appsettings.json`转换为ConfigMap：
```
> cd K8S.NETCore.ConfigMap
# 创建一个namespace，此步可选
> kubectl create namespace demo 
namespace "demo" created
# -n变量指定configmap创建到哪个namespace下
> kubectl create configmap appsettings --from-file=appsettings.json=./appsettings.json -n demo
configmap "appsettings" created
# 查看刚刚创建的configmap，-o指定输出的格式
> kubectl get configmap appsettings -n demo -o yaml
apiVersion: v1
data:
  appsettings.json: "{\r\n  \"Logging\": {\r\n    \"LogLevel\": {\r\n      \"Default\":
    \"Warning\"\r\n    }\r\n  },\r\n  \"AllowedHosts\": \"*\"\r\n}\r\n"
kind: ConfigMap
metadata:
  creationTimestamp: null
  name: appsettings
  namespace: demo
```
从上面的输出结果来看，其中包含了`\r\n`换行符，显然不是我们想要的结果。猜测是因为Windows和Linux系统换行符的差异导致的。先来插播下换行符的知识：
>CR：Carriage Return，对应ASCII中转义字符\r，表示回车
LF：Linefeed，对应ASCII中转义字符\n，表示换行
CRLF：Carriage Return & Linefeed，\r\n，表示回车并换行
众所周知，Windows操作系统采用两个字符来进行换行，即CRLF；Unix/Linux/Mac OS X操作系统采用单个字符LF来进行换行；

所以解决方式就很简单，将换行符切换为Linux系统的`\n`即可。操作方式很简单：
对于VS Code 只需要按图下所示操作即可，点击右下角的`CRLF`，选择`LF`即可。
![vs code切换换行符](https://upload-images.jianshu.io/upload_images/2799767-8f7aca93d9a8a070.png?imageMogr2/auto-orient/strip%7CimageView2/2/w/1240)


对于VS，如果VS打开json文件有下面的提示，直接切换就好。没有，可以安装[Line Endings Unifier]([https://marketplace.visualstudio.com/items?itemName=JakubBielawa.LineEndingsUnifier](https://marketplace.visualstudio.com/items?itemName=JakubBielawa.LineEndingsUnifier)
)扩展来统一处理。
![Vs inconsistent line endings](https://upload-images.jianshu.io/upload_images/2799767-90c12d22060edfdc.png?imageMogr2/auto-orient/strip%7CimageView2/2/w/1240)


```
# 先删除之前创建的configmap
> kubectl delete configmap appsettings -n demo
> kubectl create configmap appsettings --from-file=appsettings.json=./appsettings.json -n demo
configmap "appsettings" created
> kubectl get configmap appsettings -n demo -o yaml
apiVersion: v1
data:
  appsettings.json: |
    {
      "Logging": {
        "LogLevel": {
          "Default": "Warning"
        }
      },
      "AllowedHosts": "*"
    }
kind: ConfigMap
metadata:
  creationTimestamp: null
  name: appsettings
  namespace: demo
```
现在ConfigMap的格式正常了。下面我们尝试把`appsettings.Development.json`也合并到一个ConfigMap中。

```
> kubectl delete configmap appsettings -n demo
> kubectl create configmap appsettings --from-file=appsettings.json=./appsettings.json --from-file=appsettings.Development.json=./appsettings.Development.json -n demo
configmap "appsettings" created
> kubectl get configmap appsettings -n demo -o yaml
apiVersion: v1
data:
  appsettings.Development.json: |
    {
      "Logging": {
        "LogLevel": {
          "Default": "Debug",
          "System": "Information",
          "Microsoft": "Information"
        }
      }
    }
  appsettings.json: |
    {
      "Logging": {
        "LogLevel": {
          "Default": "Warning"
        }
      },
      "AllowedHosts": "*"
    }
kind: ConfigMap
metadata:
  creationTimestamp: null
  name: appsettings
  namespace: demo
```

>PS:
>1. 如果你的配置文件包含多余的空格，则生成的ConfigMap可能就会包含`\n`字符，就像这样:
`appsettings.Development.json: "{\n  \"Logging\": {\n    \"LogLevel\": {\n      \"Default\":
    \"Debug\",\n      \"System\": \"Information\",\n      \"Microsoft\": \"Information\"\n
    \   }\n  }\n} \n"`。解决办法就是保存文件时记得格式化文件就好了，或者手动删除多余空格。
>2. 创建ConfigMap的时候可以指定`--dry-run`参数进行试运行，避免直接创建到服务器。
>3. 从文件创建ConfigMap时，可以不指定Key，默认会以文件名为Key。
`kubectl create configmap appsettings --from-file=./appsettings.json --from-file=./appsettings.Development.json -n demo --dry-run -o yaml `

至此，完成了appsetting到configmap的切换。

# 应用 ConfigMap
ConfigMap的应用很简单，只需要将configmap挂载到容器内的独立目录即可。

先来看一下借助VS帮生成的Dockerfile。
```
FROM mcr.microsoft.com/dotnet/core/aspnet:2.2-stretch-slim AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/core/sdk:2.2-stretch AS build
WORKDIR /src
COPY ["K8S.NETCore.ConfigMap.csproj", ""]
RUN dotnet restore "./K8S.NETCore.ConfigMap.csproj"
COPY . .
WORKDIR "/src/."
RUN dotnet build "K8S.NETCore.ConfigMap.csproj" -c Release -o /app

FROM build AS publish
RUN dotnet publish "K8S.NETCore.ConfigMap.csproj" -c Release -o /app

FROM base AS final
WORKDIR /app
COPY --from=publish /app .
ENTRYPOINT ["dotnet", "K8S.NETCore.ConfigMap.dll"]
```
可以看出文件中定义的`WORKDIR /app`指定的工作目录为`/app`，所以需要把ConfigMap挂载到`/app`目录下。先执行`docker build -t k8s.netcore.configmap:dev .` 构建镜像。


我们来新建一个`configmap-deploy.yaml`文件配置如下：
```
apiVersion: extensions/v1beta1
kind: Deployment
metadata:
  name: k8s-configmap-demo
spec:
  selector:
    matchLabels:
      app: k8s-configmap-demo
  template:
    metadata:
      labels:
        app: k8s-configmap-demo
    spec:
      containers:
      - name: k8s-configmap-demo
        image: k8s.netcore.configmap:dev 
        imagePullPolicy: IfNotPresent
        resources:
          limits:
            memory: "128Mi"
            cpu: "500m"
        ports:
        - containerPort: 80
        volumeMounts:
          - mountPath: /app/appsettings.json
            name: test
            readOnly: true
            subPath: appsettings.json
          - mountPath: /app/appsettings.Development.json
            name: test
            readOnly: true
            subPath: appsettings.Development.json         
      volumes:
      - configMap:
          defaultMode: 420
          name: appsettings
        name: test        
```
这里有必要解释两个参数：
1. volumes：-configMap：指定引用哪个ConfigMap
2. volumeMounts：用来指定将ConfigMap中的配置挂载到容器的哪个路径
3. subPath：用来指定引用ConfigMap的哪个配置节点。

创建Deployment之前先修改下ConfigMap的配置，以方便确认最终成功从ConfigMap挂载配置。将`Logging:LogLevel:Default:`节点的默认值改为Error。
```
> kubectl edit configmap appsettings -n demo
configmap/appsettings edited
> kubectl get cm appsettings -n demo -o yaml
apiVersion: v1
data:
  appsettings.Development.json: |-
    {
      "Logging": {
        "LogLevel": {
          "Default": "Error",
          "System": "Information",
          "Microsoft": "Information"
        }
      }
    }
  appsettings.json: |
    {
      "Logging": {
        "LogLevel": {
          "Default": "Error"
        }
      },
      "AllowedHosts": "*"
    }
kind: ConfigMap
metadata:
  creationTimestamp: "2019-09-02T22:50:14Z"
  name: appsettings
  namespace: demo
  resourceVersion: "445219"
  selfLink: /api/v1/namespaces/demo/configmaps/appsettings
  uid: 07048d5a-cdd4-11e9-ad6d-00155d3a3103
```
修改完毕后，执行后续命令来创建Deployment，并验证。

```
# 创建deployment
> kubectl apply -f .\k8s-deploy.yaml -n demo
deployment.extensions/k8s-configmap-demo created
# 获取创建的pod
> kubectl get pods -n demo
NAME                                  READY   STATUS    RESTARTS   AGE
k8s-configmap-demo-7cfbdfff67-xdrcx   1/1     Running   0          12s
# 进入pod内部
> kubectl exec -it k8s-configmap-demo-7cfbdfff67-xdrcx /bin/bash -n demo
root@k8s-configmap-demo-7cfbdfff67-xdrcx:/app# cat appsettings.json
{
  "Logging": {
    "LogLevel": {
      "Default": "Error"
    }
  },
  "AllowedHosts": "*"
}
root@k8s-configmap-demo-7cfbdfff67-xdrcx:/app# cat appsettings.Development.json
{
  "Logging": {
    "LogLevel": {
      "Default": "Error",
      "System": "Information",
      "Microsoft": "Information"
    }
  }
}
```
从以上输出可以看出，默认的配置项已被ConfigMap的配置覆盖。

# 热更新
以Volume方式挂载的ConfigMap支持热更新（大概需要10s左右）。但一种情况例外，就是指定subPath的情况下，更新ConfigMap，容器中挂载的ConfigMap是不会自动更新的。
> [ A container using a ConfigMap as a subPath volume will not receive ConfigMap updates.
](https://kubernetes.io/docs/tasks/configure-pod-container/configure-pod-configmap/#mounted-configmaps-are-updated-automatically)

对于这种情况，也很好处理，将ConfigMap挂载到`/app`目录下一个单独目录就好，比如挂载到`/app/config`目录，然后修改配置文件的加载路径即可。
```
hostBuilder.ConfigureAppConfiguration((context, builder) =>
{
    builder.SetBasePath(Path.Join(AppContext.BaseDirectory, "config"))
        .AddJsonFile("appsettings.json")
        .AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.json", true, true);
});
```

# 最后
本文就.NET Core如何应用ConfigMap进行了详细的介绍。其中最关键在于appsettings.json到ConfigMap的转换，以及挂载目录的指定。希望对你有所帮助。
而至于Secret的应用，原理相通了，关键在于Secret的生成，这里就交给你自己探索了。
